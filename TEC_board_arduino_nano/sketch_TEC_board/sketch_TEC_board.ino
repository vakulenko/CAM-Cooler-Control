#include <OneWire.h>
#include <avr/interrupt.h>
#include <avr/io.h>

// User configuration defines
//-----------------------------------------------------------------------------------------------------------------------
#define SENSOR_PIN  16 //PA2=PC2

#define MOSFET_PIN  5 //PD5=PD5
#define ON          HIGH
#define OFF         LOW

// System configuration defines and macro
//-----------------------------------------------------------------------------------------------------------------------
#define USART_BAUDRATE  9600
#define BAUD_PRESCALE (((F_CPU / (USART_BAUDRATE * 16UL))) - 1)
#define BUFF_SIZE       11
#define VALUE_COUNT     1
#define HW_REVISION     1
#define SW_REVISION     1
#define CRC_POLY        0x31

#define KP              0.04

#define NO_ERROR        0
#define SENSOR_ERROR    1
#define TEMP_OFFSET     1280
#define CYCLE_DURATION  1020
#define PERCENT_RATIO   (CYCLE_DURATION / 0xff)

#define MAX_SET_TEMP    1780
#define MIN_SET_TEMP    780

#define COOLER_ON       1
#define COOLER_OFF      0

#define MAX_INIT_RETRY  60

// Global variables
//-----------------------------------------------------------------------------------------------------------------------
OneWire  ds(SENSOR_PIN);

//DS18B20
uint8_t addr[8];
uint8_t data[12];

//PID
double U = 0;
double E = 0;

//uart packet buffer
uint8_t rxBuf [BUFF_SIZE + 1];
uint8_t txBuf [BUFF_SIZE + 1];

//sensor array data
uint16_t internal_temp = 0;
uint16_t external_temp = 0;
uint16_t set_temp = 0;

//packet
boolean packetInBuffer = false;
uint8_t currentRXPacketLen = 0;

uint8_t errorCode = 0;
uint8_t errorInitCode = 0;
uint8_t coolerState;
uint8_t coolerPower = 0;

//saved value_data, powe state
uint16_t savedSetData EEMEM;
uint8_t  savedCoolerState EEMEM;

uint8_t retryCounter;

// Function definitions
//-----------------------------------------------------------------------------------------------------------------------
void setup(void) {

  retryCounter = 0;
  errorInitCode = 0;

  //MOSFET PIN
  pinMode(MOSFET_PIN, OUTPUT);
  digitalWrite(MOSFET_PIN, OFF);

  //UART
  UBRR0H = (BAUD_PRESCALE >> 8);    // Init UART baudrate
  UBRR0L = BAUD_PRESCALE;
  UCSR0B |= (1 << RXEN0) | (1 << TXEN0) | (1 << RXCIE0);   // Turn on the transmission and reception circuitry
  UCSR0C |= (1 << UCSZ00) | (1 << UCSZ01); // Use 8-bit character sizes
  //clear UART buffer
  clearBuf ();

  //DS18b20
  if ( !ds.search(addr)) {
    errorInitCode = SENSOR_ERROR;
  }
  if (OneWire::crc8(addr, 7) != addr[7]) {
    errorInitCode = SENSOR_ERROR;
  }
  if (addr[0] != 0x28) {
    errorInitCode = SENSOR_ERROR;
  }

  //set 12 bit resolution
  ds.reset();             // rest 1-Wire
  ds.select(addr);        // select DS18B20

  ds.write(0x4E);         // write on scratchPad
  ds.write(0x00);         // User byte 0 - Unused
  ds.write(0x00);         // User byte 1 - Unused
  ds.write(0x7F);         // set up en 12 bits (0x7F)

  ds.reset();             // reset 1-Wire

  //Arduinos internal temperature sensor

  // The internal temperature has to be used
  // with the internal reference of 1.1V.
  // Channel 8 can not be selected with
  // the analogRead function yet.
  // Set the internal reference and mux.
  ADMUX = (_BV(REFS1) | _BV(REFS0) | _BV(MUX3));
  ADCSRA |= _BV(ADEN);  // enable the ADC

  //read from EEPROM saved set value and coolerState state
  set_temp = eeprom_read_word (&savedSetData);
  coolerState = eeprom_read_byte(&savedCoolerState);
  //if saved settings corrupted - set it by default
  if ( (set_temp < MIN_SET_TEMP) || (set_temp > MAX_SET_TEMP) ) {
    set_temp = TEMP_OFFSET;
  }
  if ( (coolerState != COOLER_ON) && (coolerState != COOLER_OFF) ) {
    coolerState = COOLER_OFF;
  }


  //perform fist measurement
  start_measuring();
  delay(CYCLE_DURATION);
  internal_temp = GetIntTemp();
  external_temp = GetExtTemp();

  //enable interrupts
  sei();
}

//sent start conversion cmd to DS18B20
void start_measuring(void) {
  ds.reset();
  ds.select(addr);
  ds.write(0x44, 1);        // start conversion, with parasite power on at the end
}

//read DS18B20
uint16_t GetIntTemp(void) {
  uint8_t i;
  uint16_t raw, frac, temp;
  boolean sign = false;

  ds.reset();
  ds.select(addr);
  ds.write(0xBE);         // Read Scratchpad

  for ( i = 0; i < 9; i++) {           // we need 9 bytes
    data[i] = ds.read();
  }
  if (OneWire::crc8(addr, 8) != data[9]) {
    errorCode = SENSOR_ERROR;
  }
  else {
    errorCode = NO_ERROR;
  }

  //avoiding float division
  raw = (data[1] << 8) | data[0];
  if ((raw & 0x8000) != 0x0000) {
    raw = 0xffff - raw + 1;
    sign = true;
  }
  frac = 0;
  for (i = 1; i <= 8; i = i * 2) {
    if (raw & i) {
      frac += 625 * i;
    }
  }

  if (sign) {
    temp = TEMP_OFFSET - ((raw >> 4) * 10 + frac / 1000);
  }
  else {
    temp = TEMP_OFFSET + ((raw >> 4) * 10 + frac / 1000);
  }

  return temp;
}

uint16_t GetExtTemp(void)
{
  uint16_t wADC;
  ADCSRA |= _BV(ADSC);  // Start the ADC

  // Detect end-of-conversion
  while (bit_is_set(ADCSRA, ADSC));

  // Reading register "ADCW" takes care of how to read ADCL and ADCH.
  wADC = ADCW;

  // The offset of 324.31 could be wrong. It is just an indication.
  return TEMP_OFFSET + ((wADC - 312.31 ) / 1.22) * 10;
}

//Send byte thought UART
void uartTransmitByte (uint8_t data)
{
  while ( !( UCSR0A & (1 << UDRE0)) ) {
    ;
  }
  UDR0 = data;
  return;
}

//Receive byte thought UART
uint8_t uartReceiveByte (void)
{
  while ( !(UCSR0A & (1 << RXC0)) ) {
    ;
  }
  return UDR0;
}

//Packets
void clearRXBuf (void)
{
  uint8_t i;
  for (i = 0; i < BUFF_SIZE; i++) {
    rxBuf[i] = 0;
  }
  currentRXPacketLen = 0;
  return;
}

void clearTXBuf (void)
{
  uint8_t i;
  for (i = 0; i < BUFF_SIZE; i++) {
    txBuf[i] = 0;
  }
  return;
}

void clearBuf (void)
{
  uint8_t i;
  for (i = 0; i < BUFF_SIZE; i++) {
    rxBuf[i] = txBuf[i] = 0;
  }
  return;
}

//receive packet to RX buffer
ISR(USART_RX_vect, ISR_BLOCK)
{
  uint16_t i = 0;

  if (packetInBuffer) {
    i = UDR0;
  }
  else {
    rxBuf[0] = UDR0;
    currentRXPacketLen = 1;
  }

  while (!packetInBuffer)
  {
    //if we does not receive any data for 10ms - packet receive complete
    while ( !(UCSR0A & (1 << RXC0)) )
    {
      if (i == 10000) {
        packetInBuffer = true;
        break;
      }
      i++;
      _delay_loop_2(1);
    }
    if (packetInBuffer) {
      break;
    }

    rxBuf[currentRXPacketLen] = UDR0;
    currentRXPacketLen++;

    if (currentRXPacketLen >= BUFF_SIZE) {
      packetInBuffer = true;
      break;
    }
  }
  return;
}

//send packet to host
void uartSendPacket(uint8_t *data, uint8_t length)
{
  uint8_t i;
  for (i = 0; i < length; i++) {
    uartTransmitByte(txBuf[i]);
  }
  return;
}

//crc calculating function
uint8_t crc8Block(uint8_t *pcBlock, uint8_t len)
{
  uint8_t crc = 0xFF;
  uint8_t i;

  while (len--)
  {
    crc ^= *pcBlock++;

    for (i = 0; i < 8; i++) {
      crc = crc & 0x80 ? (crc << 1) ^ CRC_POLY : crc << 1;
    }
  }
  return crc;
}

//prepare TX buffer
void prepareSystemStatus (void)
{
  txBuf[0] = 'd';
  txBuf[1] = internal_temp >> 8;
  txBuf[2] = internal_temp & 0x00ff;
  txBuf[3] = external_temp >> 8;
  txBuf[4] = external_temp & 0x00ff;
  txBuf[5] = set_temp >> 8;
  txBuf[6] = set_temp & 0x00ff;
  txBuf[7] = coolerPower;
  txBuf[8] = (errorCode | errorInitCode);
  txBuf[9] = coolerState;
  txBuf[10] = crc8Block(txBuf, 10);
}

//process received packet
void processPacket(void)
{
  cli();
  //check CRC
  if (crc8Block(rxBuf, currentRXPacketLen - 1) != rxBuf[currentRXPacketLen - 1]) {
    packetInBuffer = 0;
    clearRXBuf();
    sei();
    return;
  }
  //differentiate and process packet
  switch (rxBuf[0]) {
    //if get command
    case 'g' :  {
        if (currentRXPacketLen == 2) {
          prepareSystemStatus();
          uartSendPacket(txBuf, 11);
        }
        break;
      }
    //if set command
    case 's' :  {
        if (currentRXPacketLen == 4) {
          uint16_t val;
          val = (rxBuf[1] << 8) | (rxBuf[2]);
          if ((val <= MAX_SET_TEMP) && (val >= MIN_SET_TEMP)) {
            set_temp = val;
          }
          eeprom_write_word (&savedSetData, set_temp);
        }

        break;
      }
    //if info command
    case 'i' :  {
        if (currentRXPacketLen == 2) {
          txBuf[0] = 'v';
          txBuf[1] = HW_REVISION;
          txBuf[2] = SW_REVISION;
          txBuf[3] = 2;
          txBuf[4] = VALUE_COUNT;
          txBuf[5] = crc8Block(txBuf, 5);
          uartSendPacket(txBuf, 6);
        }

        break;
      }
    //if powern ON/OFF PWM
    case 'p' :  {
        if (currentRXPacketLen == 3) {
          if ((rxBuf[1] == COOLER_OFF) || (rxBuf[1] == COOLER_ON)) {
            coolerState = rxBuf[1];
            eeprom_write_byte(&savedCoolerState, coolerState);
          }
        }
        break;
      }
    default:   {
        ;
      }
  }
  packetInBuffer = false;
  clearRXBuf();
  sei();
  return;
}

void loop(void) {
  uint8_t i;

  if (packetInBuffer != 0 ) {
    processPacket();
  }

  if (errorInitCode == NO_ERROR) {

    start_measuring();

    if (coolerState == COOLER_OFF) {
      coolerPower = 0x00;
      U = 0.0;
      E = 0.0;
      digitalWrite(MOSFET_PIN, OFF);
    }

    //P algo, do to put temperature reading to algo if error occured
    if ( (errorCode == NO_ERROR) && (coolerState == COOLER_ON) ) {
      E = (double) internal_temp - set_temp;
      U = U + KP * E;
      if (U > CYCLE_DURATION) {
        U = CYCLE_DURATION;
      } else if (U < 0.0) {
        U = 0.0;
      }

      if (U > 0.0) {
        digitalWrite(MOSFET_PIN, ON);
      }
      delay (U);
      if ( ((unsigned int) U) != CYCLE_DURATION) {
        digitalWrite(MOSFET_PIN, OFF);
      }
      delay (CYCLE_DURATION - U);

      coolerPower = ((byte)(U  / PERCENT_RATIO));
    }
    else {
      delay (CYCLE_DURATION);
    }

    internal_temp = GetIntTemp();
  }
  else
  {
    delay (CYCLE_DURATION);
    retryCounter++;
    if (retryCounter >= MAX_INIT_RETRY)
    {
      setup();
    }
  }
  external_temp = GetExtTemp();
}
