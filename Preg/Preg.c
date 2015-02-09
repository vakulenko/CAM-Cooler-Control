#include	<avr/io.h>
#include	<avr/interrupt.h>
#include 	<util/delay.h>
#include	<avr/eeprom.h>
#include 	<stdint.h>

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
#define PWM_PORT	PORTD
#define PWM_DDR		DDRD
#define PWM_PIN		5

#define PWM_ON		1
#define PWM_OFF		0

//ds18b20 cmd
#define SKIP_ROM			0xcc
#define	START_CONVERSION	0x44
#define GET_DATA			0xbe

//uart baudrate settings
#define UART_BAUDRATE_H 0
#define UART_BAUDRATE_L	51	//9600 bps at 8MHz

//CRC
#define CRC_POLY 0x31

//Proportional regulator
#define Kp_INIT	0.02

//uart packet buffer
volatile uint8_t rx_buf [BUFF_SIZE+1];
volatile uint8_t tx_buf [BUFF_SIZE+1];

//sensor array data
volatile uint16_t sensor_data [SENSOR_COUNT];
volatile uint16_t value_data [VALUE_COUNT];

//packet 
volatile uint8_t packet_received=0;
volatile uint8_t current_rx_packet_len=0;

volatile uint8_t error_code=0;
volatile uint8_t pwm_state;

//PID
volatile double U, Kp, E;
volatile uint8_t correction=0;

//saved value_data, powe state
uint16_t saved_value EEMEM;
uint8_t	saved_pwm_state EEMEM;

//------------------------------------------------------------------------------------
//UART

void uart_init(void)
{
	UBRRH = UART_BAUDRATE_H;		// Init UART baudrate
	UBRRL = UART_BAUDRATE_L;

	UCSRB = (1<<RXEN)|(1<<TXEN)|(1 << RXCIE);	// TX, RX enable, RX interrupt enable
	UCSRC = (1<<URSEL)|(1<<UCSZ1)|(1<<UCSZ0);
	return;
}

//Send byte thought UART
void uart_transmit_byte (uint8_t data)
{
	while ( !( UCSRA & (1<<UDRE)) );
	UDR = data;
	return;
}

//Receive byte thought UART
unsigned char uart_receive (void)
{

	while ( !(UCSRA & (1<<RXC)) )
	;
	return UDR;
}

//Transmit string to UART
void uart_transmit_message(char* msg)
{ unsigned char i;
	i=0;

	while ((i<256)&(msg[i]!=0x00) )
	{
		uart_transmit_byte(msg[i]);
		i++;
	}
	return;
}

//------------------------------------------------------------------------------------
//PWM

void init_pwm (void)
{
	PWM_PORT&=~(1<<PWM_PIN);
	PWM_DDR|=(1<<PWM_PIN);

	TCCR1A|=(1<<WGM10);
	TCCR1B|=(1<<WGM12)|(1<<CS10);

	OCR1AL=0x00;
	OCR1BL=0x00;
	return;
}

void off_pwm()
{
	TCCR1A&=~(1<<COM1A1);
	return;
}

void on_pwm()
{
	TCCR1A|=(1<<COM1A1);
	return;
}

void set_pwm (uint8_t data)
{
	OCR1AL=data;
	if (data==0) off_pwm();
	else on_pwm();

	return;
}

uint8_t get_pwm (void)
{
	return OCR1AL;
}

//------------------------------------------------------------------------------------
//Packets
void clear_rx_buf (void)
{
	uint8_t i;
	for (i=0;i<BUFF_SIZE;i++)
	rx_buf[i]=0;
	current_rx_packet_len=0;
	return;
}

void clear_tx_buf (void)
{
	uint8_t i;
	for (i=0;i<BUFF_SIZE;i++)
	tx_buf[i]=0;
	return;
}

void clear_buf (void)
{
	uint8_t i;
	for (i=0;i<BUFF_SIZE;i++)
	rx_buf[i]=tx_buf[i]=0;
	return;
}

//receive packet to RX buffer
ISR(USART_RXC_vect)
{
	uint16_t i=0;

	if (packet_received!=0)
	{
		i=UDR;
		return;
	}

	rx_buf[0]=UDR;
	current_rx_packet_len=1;

	while (1)
	{
		while ( !(UCSRA & (1<<RXC)) )
		{
			if (i==10000)
			{
				packet_received=1;
				return;
			}
			i++;
			_delay_loop_2(1);
		}
		rx_buf[current_rx_packet_len]=UDR;
		current_rx_packet_len++;

		if (current_rx_packet_len>=BUFF_SIZE)
		{
			packet_received=1;
			return;
		}
	}
}

//send packet to host
void uart_send_packet(uint8_t *data, uint8_t length)
{
	uint8_t i;
	for (i=0;i<length;i++)
	uart_transmit_byte(tx_buf[i]);
	return;
}

//crc calculating function
uint8_t crc8_block(uint8_t *pcBlock, uint8_t len)
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
void prepare_system_status (void)
{
	tx_buf[0]='d';
	tx_buf[1]= sensor_data[0]>>8;
	tx_buf[2]= sensor_data[0]&0x00ff;
	tx_buf[3]= sensor_data[1]>>8;
	tx_buf[4]= sensor_data[1]&0x00ff;
	tx_buf[5]= value_data[0]>>8;
	tx_buf[6]= value_data[0]&0x00ff;
	tx_buf[7]= get_pwm();
	tx_buf[8]= error_code;
	tx_buf[9]= pwm_state;
	tx_buf[10]=crc8_block(tx_buf,10);
}

//process received packet
void process_packet(void)
{
	cli();
	//check CRC
	if (crc8_block(rx_buf,current_rx_packet_len-1)!=rx_buf[current_rx_packet_len-1])
	{
		packet_received=0;
		clear_rx_buf();
		sei();
		return;
	}
	//differentiate and process packet
	switch (rx_buf[0]) {
		//if get command
		case 'g' : 	{
			if (current_rx_packet_len==2)
			{
				prepare_system_status();
				uart_send_packet(tx_buf,11);	
			}			
			break;
		}
		//if set command
		case 's' : 	{
			if (current_rx_packet_len==4)
			{
				uint16_t val;

				val=(rx_buf[1]<<8)|(rx_buf[2]);
				if ((val<=1780)&&(val>=780)) value_data[0]=val;
				eeprom_write_word (&saved_value, value_data[0]);
			}
			
			break;
		}
		//if info command
		case 'i' :	{
			if (current_rx_packet_len==2)
			{
				tx_buf[0]='v';
				tx_buf[1]=HW_REVISION;
				tx_buf[2]=SW_REVISION;
				tx_buf[3]=SENSOR_COUNT;
				tx_buf[4]=VALUE_COUNT;
				tx_buf[5]=crc8_block(tx_buf,5);
				uart_send_packet(tx_buf,6);
			}

			break;
		}
		//if powern ON/OFF PWM
		case 'p' : 	{
		if (current_rx_packet_len==3)
		{
			if ((rx_buf[1]==PWM_OFF)||(rx_buf[1]==PWM_ON))
			{
				pwm_state=rx_buf[1];
				eeprom_write_word (&saved_pwm_state, pwm_state);
			}		
		}
		break;
		}
		default:
		{
			;
		}
	}

	packet_received=0;
	clear_rx_buf();
	sei();
	return;
}

//------------------------------------------------------------------------------------
//DS18B20

uint8_t present_ds18b20(uint8_t sensor_num)
{	uint8_t res, sensor_pin;

	if (sensor_num==0)	sensor_pin=SENSOR0_PIN;
	else sensor_pin=SENSOR1_PIN;
	
	SENSOR_DDR|=(1<<sensor_pin);
	_delay_loop_2(950);         //Pause 480mks

	SENSOR_DDR&=~(1<<sensor_pin);
	_delay_loop_2(130);          //Pause 70mks

	if ((SENSOR_PIN&(1<<sensor_pin)) == 0x00) res=1;  //if present, res=1
	else res=0;  // else 0
	
	_delay_loop_2(820);          //pause 410mks
	return res;
}

void send_ds18b20(uint8_t command, uint8_t sensor_num)
{	uint8_t i, data, sensor_pin;

	if (sensor_num==0)	sensor_pin=SENSOR0_PIN;
	else sensor_pin=SENSOR1_PIN;
	data=command;

	for(i=0;i<8;i++)
	{
		if ((data&0x01)==0x01) {    //Send 1 on SDA
			SENSOR_DDR|=(1<<sensor_pin);
			_delay_loop_2(4);		//pause 6mks
			SENSOR_DDR&=~(1<<sensor_pin);
			_delay_loop_2(120);	//pause 64mks
		}
		else {                   	//Send 0 on SDA
			SENSOR_DDR|=(1<<sensor_pin);
			_delay_loop_2(110);    //pause 60mks
			SENSOR_DDR&=~(1<<sensor_pin);
			_delay_loop_2(4);		//pause 10mks
		}
		data=data>>1;
	}
	return;
}

uint16_t receive_ds18b20(uint8_t sensor_num)
{	uint8_t i, sensor_pin;
	uint16_t res=0;

	if (sensor_num==0)	sensor_pin=SENSOR0_PIN;
	else sensor_pin=SENSOR1_PIN;

	for(i=0;i<16;i++)
	{
		SENSOR_DDR|=(1<<sensor_pin);
		_delay_loop_2(4);       		//Pause 6mks
		SENSOR_DDR&=~(1<<sensor_pin);
		_delay_loop_1(12);           	//Pause 9mks

		if ((SENSOR_PIN & (1<<sensor_pin))==0x00) res&=~_BV(i);	//If 0 on SDA
		else 	res|=_BV(i);	    //IF 1 on SDA
		
		_delay_loop_2(100);      		//Pause 55mks
	}
	return res;
}
//------------------------------------------------------------------------------------

int main(void)
{
	uint8_t i, sign, first_conv=_YES;
	uint16_t val, fract;
	
	//read from EEPROM saved value & PWM state
	value_data[0]= eeprom_read_word (&saved_value);
	pwm_state = eeprom_read_byte(&saved_pwm_state);

	//init variables
	for (i=0;i<SENSOR_COUNT;i++)
	sensor_data[i]=0;

	clear_buf ();

	U=0.0;
	Kp=Kp_INIT;
	E=0;

	//Init ports, UART, PWM
	SENSOR_PORT&=~((1<<SENSOR0_PIN)|(1<<SENSOR1_PIN));     	
	SENSOR_DDR&=~((1<<SENSOR0_PIN)|(1<<SENSOR1_PIN));
	init_pwm();
	uart_init();
	
	sei();

	while (1)
	{

		_delay_loop_2(0xffff);

		//delay 10ms
		//_delay_ms(10);
		i++;

		if (packet_received!=0) process_packet();

		if (i>=35)
		{
			i=0;

			//send command from start measurement to sensors
			error_code=0;
			for (i=0;i<SENSOR_COUNT;i++)
			{
				if (present_ds18b20(i)==1)
				{
					send_ds18b20(SKIP_ROM,i);
					send_ds18b20(START_CONVERSION,i);
					error_code=0;
				}
				else error_code|=(1<<i);
			}

			if (first_conv==_YES) first_conv=_NO;
			else
			{
		 
				//receive measured data from sensors
				for (i=0;i<SENSOR_COUNT;i++)
				{
					if (present_ds18b20(i)==1)
					{
						send_ds18b20(SKIP_ROM,i);
						send_ds18b20(GET_DATA,i);
						val=receive_ds18b20(i);

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

						sensor_data[i]=val;
					}
					else error_code|=(1<<i);
				}

				//if PWM if OFF - clear all variables and off PWM
				if (pwm_state==PWM_OFF)
				{
					off_pwm();
					set_pwm(0x00);
					U=0.0;
					E=0.0;
					correction=0;
				}
				else if (get_pwm()!=0) on_pwm();

				//If no errors at sensor[0] and PWM is ON - calculate and set correction
				if (((error_code & 0x01)==0)&&(pwm_state==PWM_ON))
				{
					E=(double) sensor_data[0]-value_data[0];

					U=U+Kp*E;

					if (U>255.0) 	U=255.0;
					if (U<=0.0) 	U=0.0;
		
					correction=(uint8_t) U;
					set_pwm(correction); 				
				}

			}
		}
	}
}
