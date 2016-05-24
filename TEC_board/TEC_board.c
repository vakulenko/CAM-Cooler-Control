#include	<avr/io.h>
#include	<avr/interrupt.h>
#include 	<util/delay.h>
#include	<avr/eeprom.h>
#include 	<stdint.h>

////////////////////////////////////////////////////////////////////
//Start User define parameters
////////////////////////////////////////////////////////////////////
//mode of operation
//0 - mode disabled, 1 - enabled;
//Hardware PWM 32kHz
#define PWM_MODE		0
//Software PWM 3Hz
#define SLOW_PWM_MODE	1
//Thermoswitch
#define THERMOSTAT_MODE	0

//Module save previous state in EEPROM (on/off; setData[0])
#define STANDALONE_MODE	1

//Thermoswitch hysterezis *0.1C
#define HYSTERESIS		1

//Proportional regulator
#define KP		0.02
#define KPSLOW	0.01
////////////////////////////////////////////////////////////////////
//End User define parameters
////////////////////////////////////////////////////////////////////

#define	BUFF_SIZE		11
#define	SENSOR_COUNT	2
#define VALUE_COUNT		1
#define HW_REVISION		1
#define SW_REVISION		1
#define OFFSET 			1280

#define _YES			1
#define	_NO				0

//temp sensors
#define SENSOR_PORT	PORTC
#define SENSOR_DDR	DDRC
#define SENSOR_PIN	PINC

#define	SENSOR0_PIN	2		//sensor at TEC
#define	SENSOR1_PIN	3		//sensor at air

//PWM
#define TEC_PORT	PORTD
#define TEC_DDR		DDRD
#define TEC_PIN		5

#define COOLER_ON	1
#define COOLER_OFF	0

//ds18b20 cmd
#define SKIP_ROM			0xcc
#define	START_CONVERSION	0x44
#define GET_DATA			0xbe

//uart baudrate settings
#define USART_BAUDRATE 9600
#define BAUD_PRESCALE (((F_CPU / (USART_BAUDRATE * 16UL))) - 1)

//CRC
#define CRC_POLY 0x31

//uart packet buffer
uint8_t rxBuf [BUFF_SIZE+1];
uint8_t txBuf [BUFF_SIZE+1];

//sensor array data
uint16_t sensorData [SENSOR_COUNT];
uint16_t setData [VALUE_COUNT];

//packet 
uint8_t packetReceived=0;
uint8_t currentRXPacketLen=0;

uint8_t errorCode=0;
uint8_t coolerState;
uint8_t coolerPower=0;

//PID
double U, E;

//saved value_data, powe state
uint16_t savedSetData EEMEM;
uint8_t	savedCoolerState EEMEM;

//------------------------------------------------------------------------------------
//UART

void uartInit(void)
{
	UBRRH = (BAUD_PRESCALE >> 8);		// Init UART baudrate
	UBRRL = BAUD_PRESCALE;

	UCSRB = (1<<RXEN)|(1<<TXEN)|(1 << RXCIE);	// TX, RX enable, RX interrupt enable
	UCSRC = (1<<URSEL)|(1<<UCSZ1)|(1<<UCSZ0);
	return;
}

//Send byte thought UART
void uartTransmitByte (uint8_t data)
{
	while ( !( UCSRA & (1<<UDRE)) );
	UDR = data;
	return;
}

//Receive byte thought UART
unsigned char uartReceiveByte (void)
{

	while ( !(UCSRA & (1<<RXC)) )
	;
	return UDR;
}

//Transmit string to UART
void uartTransmitMessage(char* msg)
{ unsigned char i;
	i=0;

	while ((i<256)&(msg[i]!=0x00) )
	{
		uartTransmitByte(msg[i]);
		i++;
	}
	return;
}

//------------------------------------------------------------------------------------
//PWM

void initPWM (void)
{
	TEC_PORT&=~(1<<TEC_PIN);
	TEC_DDR|=(1<<TEC_PIN);

	TCCR1A|=(1<<WGM10);
	TCCR1B|=(1<<WGM12)|(1<<CS10);

	OCR1AL=0x00;
	OCR1BL=0x00;
	return;
}

void offPWM()
{
	TCCR1A&=~(1<<COM1A1);
	return;
}

void onPWM()
{
	TCCR1A|=(1<<COM1A1);
	return;
}

void setPWM (uint8_t data)
{
	OCR1AL=data;
	if (data==0) offPWM();
	else onPWM();

	return;
}

uint8_t getPWM (void)
{
	return OCR1AL;
}

//------------------------------------------------------------------------------------
//Packets
void clearRXBuf (void)
{
	uint8_t i;
	for (i=0;i<BUFF_SIZE;i++)
	rxBuf[i]=0;
	currentRXPacketLen=0;
	return;
}

void clearTXBuf (void)
{
	uint8_t i;
	for (i=0;i<BUFF_SIZE;i++)
	txBuf[i]=0;
	return;
}

void clearBuf (void)
{
	uint8_t i;
	for (i=0;i<BUFF_SIZE;i++)
	rxBuf[i]=txBuf[i]=0;
	return;
}

//receive packet to RX buffer
ISR(USART_RXC_vect)
{
	uint16_t i=0;

	if (packetReceived!=0)
	{
		i=UDR;
		return;
	}

	rxBuf[0]=UDR;
	currentRXPacketLen=1;

	while (1)
	{
		while ( !(UCSRA & (1<<RXC)) )
		{
			if (i==10000)
			{
				packetReceived=1;
				return;
			}
			i++;
			_delay_loop_2(1);
		}
		rxBuf[currentRXPacketLen]=UDR;
		currentRXPacketLen++;

		if (currentRXPacketLen>=BUFF_SIZE)
		{
			packetReceived=1;
			return;
		}
	}
}

//send packet to host
void uartSendPacket(uint8_t *data, uint8_t length)
{
	uint8_t i;
	for (i=0;i<length;i++)
	uartTransmitByte(txBuf[i]);
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
		
		for (i = 0; i < 8; i++)
		crc = crc & 0x80 ? (crc << 1) ^ CRC_POLY : crc << 1;
	}	
	return crc;
}

//prepare TX buffer
void prepareSystemStatus (void)
{
	txBuf[0]='d';
	txBuf[1]= sensorData[0]>>8;
	txBuf[2]= sensorData[0]&0x00ff;
	txBuf[3]= sensorData[1]>>8;
	txBuf[4]= sensorData[1]&0x00ff;
	txBuf[5]= setData[0]>>8;
	txBuf[6]= setData[0]&0x00ff;
	#if PWM_MODE == 1
		txBuf[7]= getPWM();
	#else
		txBuf[7]=coolerPower;
	#endif
	txBuf[8]= errorCode;
	txBuf[9]= coolerState;
	txBuf[10]=crc8Block(txBuf,10);
}

//process received packet
void processPacket(void)
{
	cli();
	//check CRC
	if (crc8Block(rxBuf,currentRXPacketLen-1)!=rxBuf[currentRXPacketLen-1])
	{
		packetReceived=0;
		clearRXBuf();
		sei();
		return;
	}
	//differentiate and process packet
	switch (rxBuf[0]) {
		//if get command
		case 'g' : 	{
			if (currentRXPacketLen==2)
			{
				prepareSystemStatus();
				uartSendPacket(txBuf,11);	
			}			
			break;
		}
		//if set command
		case 's' : 	{
			if (currentRXPacketLen==4)
			{
				uint16_t val;

				val=(rxBuf[1]<<8)|(rxBuf[2]);
				if ((val<=1780)&&(val>=780)) setData[0]=val;
				#if STANDALONE_MODE == 1
					eeprom_write_word (&savedSetData, setData[0]);
				#endif				
			}
			
			break;
		}
		//if info command
		case 'i' :	{
			if (currentRXPacketLen==2)
			{
				txBuf[0]='v';
				txBuf[1]=HW_REVISION;
				txBuf[2]=SW_REVISION;
				txBuf[3]=SENSOR_COUNT;
				txBuf[4]=VALUE_COUNT;
				txBuf[5]=crc8Block(txBuf,5);
				uartSendPacket(txBuf,6);
			}

			break;
		}
		//if powern ON/OFF PWM
		case 'p' : 	{
		if (currentRXPacketLen==3)
		{
			if ((rxBuf[1]==COOLER_OFF)||(rxBuf[1]==COOLER_ON))
			{
				coolerState=rxBuf[1];
				#if STANDALONE_MODE == 1 
					eeprom_write_byte(&savedCoolerState, coolerState);	
				#endif				
			}		
		}
		break;
		}
		default:
		{
			;
		}
	}
	packetReceived=0;
	clearRXBuf();
	sei();
	return;
}

//------------------------------------------------------------------------------------
//DS18B20

uint8_t presentDS18b20(uint8_t sensor_num)
{	uint8_t res, sensor_pin;

	if (sensor_num==0)	sensor_pin=SENSOR0_PIN;
	else sensor_pin=SENSOR1_PIN;
	
	SENSOR_DDR|=(1<<sensor_pin);
	_delay_us (520);

	SENSOR_DDR&=~(1<<sensor_pin);
	_delay_us(30);
	
	if ((SENSOR_PIN&(1<<sensor_pin)) == 0x00) res=1;  
	else res=0;  
	
	_delay_us(490);
	return res;
}

void sendDS18b20(uint8_t command, uint8_t sensor_num)
{	uint8_t i, data, sensor_pin;

	if (sensor_num==0)	sensor_pin=SENSOR0_PIN;
	else sensor_pin=SENSOR1_PIN;
	data=command;

	for(i=0;i<8;i++)
	{
		if ((data&0x01)==0x01) {    //Send 1 on SDA
			SENSOR_DDR|=(1<<sensor_pin);
			_delay_us(15);
			SENSOR_DDR&=~(1<<sensor_pin);
			_delay_us(75);
		}
		else {                   	//Send 0 on SDA
			SENSOR_DDR|=(1<<sensor_pin);
			_delay_us(75);
			SENSOR_DDR&=~(1<<sensor_pin);
			_delay_us(15);
		}
		data=data>>1;
	}
	return;
}

uint16_t receiveDS18b20(uint8_t sensor_num)
{	uint8_t i, sensor_pin;
	uint16_t res=0;

	if (sensor_num==0)	sensor_pin=SENSOR0_PIN;
	else sensor_pin=SENSOR1_PIN;

	for(i=0;i<16;i++)
	{
		SENSOR_DDR|=(1<<sensor_pin);		
		_delay_us(9);
		SENSOR_DDR&=~(1<<sensor_pin);		
		_delay_us(12);

		if ((SENSOR_PIN & (1<<sensor_pin))==0x00) res&=~_BV(i);	//If 0 on SDA
		else 	res|=_BV(i);	    //IF 1 on SDA
				
		_delay_us(75);
	}
	return res;
}

//------------------------------------------------------------------------------------
int main(void)
{
	uint8_t i, sign, firstConv=_YES;
	uint16_t val, fract;	
	//read from EEPROM saved value & coolerState state
	#if STANDALONE_MODE == 1
		setData[0]= eeprom_read_word (&savedSetData);
		coolerState = eeprom_read_byte(&savedCoolerState);
		//if saved settings corrupted - set it by default
		if ( (setData[0]<780)||(setData[0]>1780) ) setData[0]=1730;
		if ( (coolerState!=COOLER_ON)&&(coolerState!=COOLER_OFF) ) coolerState=COOLER_OFF;
	#else
		setData[0]=1730;
		coolerState=COOLER_OFF;
	#endif
	//init variables
	for (i=0;i<SENSOR_COUNT;i++)
		sensorData[i]=0;
	clearBuf ();
	U=0.0;
	E=0;
	//Init ports, UART, PWM
	SENSOR_PORT&=~((1<<SENSOR0_PIN)|(1<<SENSOR1_PIN));     	
	SENSOR_DDR&=~((1<<SENSOR0_PIN)|(1<<SENSOR1_PIN));		
 
	#if PWM_MODE == 1
		initPWM();
	#else
		TEC_PORT&=~(1<<TEC_PIN);
		TEC_DDR|=(1<<TEC_PIN);
	#endif
	uartInit();	
	sei();

//##############################################################
	#if PWM_MODE == 1
	while (1)
	{
		_delay_ms(10);
		i++;
		if (packetReceived!=0) processPacket();
		if (i>=35)
		{
			i=0;
			//send command from start measurement to sensors
			errorCode=0;
			for (i=0;i<SENSOR_COUNT;i++)
			{
				if (presentDS18b20(i)==1)
				{
					sendDS18b20(SKIP_ROM,i);
					sendDS18b20(START_CONVERSION,i);
				}
				else errorCode|=(1<<i);
			}
			if (firstConv==_YES) firstConv=_NO;
			else
			{		 
				//receive measured data from sensors
				for (i=0;i<SENSOR_COUNT;i++)
				{
					if (presentDS18b20(i)==1)
					{
						sendDS18b20(SKIP_ROM,i);
						sendDS18b20(GET_DATA,i);
						val=receiveDS18b20(i);
						if ((val&0x8000)!=0x00)
						{
							sign=1;
							val=0xffff-val+1;
						}
						else sign=0;
						fract=0;
						if ((val&0x01)!=0x00) fract=fract+65;
						if ((val&0x02)!=0x00) fract=fract+125;
						if ((val&0x04)!=0x00) fract=fract+250;
						if ((val&0x08)!=0x00) fract=fract+500;
						val=(val>>4)*10+fract/100;
						if (sign==1) val=OFFSET-val;
						else val=val+OFFSET;
						sensorData[i]=val;
					}
					else errorCode|=(1<<i);
				}
				//if coolerState is OFF - clear all variables and off PWM
				if (coolerState==COOLER_OFF)
				{
					offPWM();
					setPWM(0x00);
					U=0.0;
					E=0.0;
				}				
				else if (getPWM()!=0) onPWM();
				//If no errors at sensor[0] and coolerState is ON - calculate and set correction
				if (((errorCode & 0x01)==0)&&(coolerState==COOLER_ON))
				{
					E=(double) sensorData[0]-setData[0];

					U=U+KP*E;

					if (U>255.0) 	U=255.0;
					if (U<=0.0) 	U=0.0;		
					setPWM((uint8_t) U); 				
				}
			}
		}
	}
	#endif

//##############################################################
	#if SLOW_PWM_MODE == 1
	while (1)
	{
		if (packetReceived!=0) processPacket();
		errorCode=0;
		for (i=0;i<SENSOR_COUNT;i++)
		{
			if (presentDS18b20(i)==1)
			{
				sendDS18b20(SKIP_ROM,i);
				sendDS18b20(START_CONVERSION,i);
			}
			else errorCode|=(1<<i);
		}
		if (firstConv==_YES) firstConv=_NO;
		else
		{		 
			//receive measured data from sensors
			for (i=0;i<SENSOR_COUNT;i++)
			{
				if (presentDS18b20(i)==1)
				{
					sendDS18b20(SKIP_ROM,i);
					sendDS18b20(GET_DATA,i);
					val=receiveDS18b20(i);
					if ((val&0x8000)!=0x00)
					{
						sign=1;
						val=0xffff-val+1;
					}
					else sign=0;
					fract=0;
					if ((val&0x01)!=0x00) fract=fract+65;
					if ((val&0x02)!=0x00) fract=fract+125;
					if ((val&0x04)!=0x00) fract=fract+250;
					if ((val&0x08)!=0x00) fract=fract+500;
					val=(val>>4)*10+fract/100;
					if (sign==1) val=OFFSET-val;
					else val=val+OFFSET;
					sensorData[i]=val;
				}
				else errorCode|=(1<<i);
			}
			//if coolerState is OFF - clear all variables
			if (coolerState==COOLER_OFF) 
			{
				coolerPower=0x00;
				U=0.0;
				E=0.0;
				TEC_PORT&=~(1<<TEC_PIN);
			}
			//If no errors at sensor[0] and coolerState is ON - calculate and set software PWM
			if (coolerState==COOLER_ON)
				{
					E=(double) sensorData[0]-setData[0];

					U=U+KPSLOW*E;

					if (U>255.0) 	U=255.0;
					if (U<=0.0) 	U=0.0;		
					
					if (U>0.0) TEC_PORT|=(1<<TEC_PIN);	
					_delay_ms(U);								
					if (((uint8_t) U)!=255)TEC_PORT&=~(1<<TEC_PIN);
					_delay_ms(255-U);

					coolerPower=((uint8_t)U);
				}
			}		
	}	
	#endif

//##############################################################
	#if THERMOSTAT_MODE == 1
	while (1)
	{
		_delay_ms(10);
		i++;
		if (packetReceived!=0) processPacket();
		if (i>=35)
		{
			i=0;
			//send command from start measurement to sensors
			errorCode=0;
			for (i=0;i<SENSOR_COUNT;i++)
			{
				if (presentDS18b20(i)==1)
				{
					sendDS18b20(SKIP_ROM,i);
					sendDS18b20(START_CONVERSION,i);
				}
				else errorCode|=(1<<i);
			}
			if (firstConv==_YES) firstConv=_NO;
			else
			{		 
				//receive measured data from sensors
				for (i=0;i<SENSOR_COUNT;i++)
				{
					if (presentDS18b20(i)==1)
					{
						sendDS18b20(SKIP_ROM,i);
						sendDS18b20(GET_DATA,i);
						val=receiveDS18b20(i);
						if ((val&0x8000)!=0x00)
						{
							sign=1;
							val=0xffff-val+1;
						}
						else sign=0;
						fract=0;
						if ((val&0x01)!=0x00) fract=fract+65;
						if ((val&0x02)!=0x00) fract=fract+125;
						if ((val&0x04)!=0x00) fract=fract+250;
						if ((val&0x08)!=0x00) fract=fract+500;
						val=(val>>4)*10+fract/100;
						if (sign==1) val=OFFSET-val;
						else val=val+OFFSET;
						sensorData[i]=val;
					}
					else errorCode|=(1<<i);
				}
				//if coolerState is OFF - clear all variables and off PIN
				if (coolerState==COOLER_OFF) 
					{
						TEC_PORT&=~(1<<TEC_PIN);
						coolerPower=0x00;
					}
					else
					{
						TEC_PORT|=(1<<TEC_PIN);																					
						coolerPower=0xff;
					}
				//If no errors at sensor[0] and coolerState is ON - calculate and set PIN
				if (coolerState==COOLER_ON)
				{
					if (sensorData[0]>(setData[0]+HYSTERESIS)) 
					{
						TEC_PORT|=(1<<TEC_PIN);
						coolerPower=0xff;
					}
					else if (sensorData[0]<(setData[0]))
					{
						TEC_PORT&=~(1<<TEC_PIN);
						coolerPower=0x00;
					}
				}
			}
		}		
	}
	#endif
}
