using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Timers;
using System.IO;                // work with files
using System.IO.Ports;          // work with COM ports
using System.Xml.Serialization; // class savings

namespace TEC_control
{
    public partial class main_form : Form
    {
        private SerialPort serialport;

        private int pwm = 0;
        private int err_code = 0;
        private bool pwm_state = false;
        private double external_temp = 0.0;
        private double internal_temp = 0.0;
        private double val_temp = 0.0;

        private bool IsConnected = false;

        private byte[] info_cmd = { 0x69, 0x9f };
        private byte[] get_cmd = { 0x67, 0x80 };
        private byte[] set_cmd = { 0x73, 0x00, 0x00, 0x00 };
        private byte[] pwr_cmd = { 0x70, 0x00, 0x00 };
        private byte currentCmd = 0x00;

        private const int baudrate = 9600;

        private const string settings_filename = "tec_control_settings.xml";

        private const byte buffer_size = 11;
        private const byte responce_packet_size = 11;
        private const byte info_packet_size = 6;

        private const byte HWREV = 0x01;
        private const byte SWREV = 0x01;
        private const byte SENSOR_CNT = 0x02;
        private const byte VAL_CNT = 0x01;
        private const byte SET_CMD = 0x00;
        private const byte PWR_CMD = 0x01;
        private const byte PACKET_SKIP_GRAYOUT = 0x03;

        private const int temp_offset = 128;

        private const byte _ON = 0x01;
        private const byte _OFF = 0x00;

        private static System.Timers.Timer get_timer;

        byte[] rx_buf;

        private static System.Timers.Timer slowCoolingTimer;
        private static double slowCoolingInterm = 0.0;
        private static bool slowCoolingCoolingDirection = true; //true==cooling, false==heating
        private static double slowCoolingTarger = 0.0;
        private static bool slowCoolingReady = false;
        private static double slowCoolingTransferValue = 0.0;

        private static System.Timers.Timer connectTimer;
        private static System.Timers.Timer setTimer;

        private DateTime lastSent;
        private DateTime lastReceive = DateTime.Now;

        private bool helloFlag = false;

        private int waitPacket = 0;

        public main_form()
        {
            InitializeComponent();

            rx_buf = new byte[buffer_size];

            //Get data from controller every second
            get_timer = new System.Timers.Timer(1000); //1 sec
            get_timer.Enabled = false;
            get_timer.Elapsed += get_timer_tick;

            //Adjust Set Temp for slow cooling every minute
            slowCoolingTimer = new System.Timers.Timer(60000); //1 min
            slowCoolingTimer.Enabled = false;
            slowCoolingTimer.Elapsed += slowCoolingTimerTick;

            //
            connectTimer = new System.Timers.Timer(100); //0.1 sec
            connectTimer.Enabled = false;
            connectTimer.Elapsed += connectTimerTick;

            //
            setTimer = new System.Timers.Timer(1200); //1.2 sec
            setTimer.Enabled = false;
            setTimer.Elapsed += setTimerTick;

            string[] comPorts = SerialPort.GetPortNames();
            for (int i = 0; i < comPorts.Length; i++)
            {
                port_combobox.Items.Add(comPorts[i]);
            }

            //extract port, temperature settings
            if (File.Exists(settings_filename))
            {
                try
                {
                    using (Stream stream = new FileStream(settings_filename, FileMode.Open))
                    {
                        XmlSerializer serializer = new XmlSerializer(typeof(iniSettings));

                        iniSettings pid_settings = (iniSettings)serializer.Deserialize(stream);
                        //check port, temperature validity                        
                        if (((decimal)pid_settings.set_temp < setTempNumericUpDown.Minimum) || ((decimal)pid_settings.set_temp > setTempNumericUpDown.Maximum))
                        {
                            pid_settings.set_temp = (double)setTempNumericUpDown.Maximum;
                        }
                        setTempNumericUpDown.Value = (decimal)pid_settings.set_temp;
                        if (((decimal)pid_settings.slowCoolingSpeed < slowCoolingNumericUpDown.Minimum) || ((decimal)pid_settings.set_temp > slowCoolingNumericUpDown.Maximum))
                        {
                            pid_settings.slowCoolingSpeed = (double)slowCoolingNumericUpDown.Minimum;
                        }
                        slowCoolingNumericUpDown.Value = (decimal)pid_settings.slowCoolingSpeed;
                        slowCoolingCheckBox.Checked = pid_settings.slowCooling;

                        for (int i = 0; i < comPorts.Length; i++)
                        {
                            if (comPorts[i] == pid_settings.port)
                            {
                                port_combobox.SelectedIndex = i;
                            }
                        }
                    }
                }
                catch
                {
                    System.Windows.Forms.MessageBox.Show(settings_filename + " damaged, use settings by default.");
                    port_combobox.SelectedIndex = 0;
                    setTempNumericUpDown.Value = setTempNumericUpDown.Maximum;
                    slowCoolingCheckBox.Checked = false;
                    slowCoolingNumericUpDown.Value = slowCoolingNumericUpDown.Minimum;
                }
            }
        }

        private void FaultDisconnect()
        {
            get_timer.Enabled = false;
            setTimer.Enabled = false;
            connectTimer.Enabled = false;
            try
            {
                serialport.Close();
            }
            catch
            {
                ;
            }
            connect_button.Text = "Connect";
            IsConnected = false;
            status_label.Text = "Disconnected";
            port_combobox.Enabled = !IsConnected;
            control_groupbox.Enabled = IsConnected;
        }

        private void get_timer_tick(object sender, EventArgs e)
        {
            lastSent = DateTime.Now;

            //detect 30 sec timeout
            if ((lastSent - lastReceive).TotalSeconds > 30)
            {
                FaultDisconnect();
                System.Windows.Forms.MessageBox.Show("Device does not responding more that 30 seconds. Check connection.");
                return;
            }
            
            string[] comPorts;
            bool comPortEjected = true;
            comPorts = SerialPort.GetPortNames();
            int j;
            for (j = 0; j < comPorts.Length; j++)
            {
                if (comPorts[j] == port_combobox.SelectedItem.ToString()) 
                {
                    comPortEjected = false;
                }
            }

            if (comPortEjected)
            {
                FaultDisconnect();
                System.Windows.Forms.MessageBox.Show("Serial port unexpectedly ejected from system.");
                return;
            }
            

            if (slowCoolingReady && slowCoolingCheckBox.Checked)
            {
                slowCoolingReady = false;
                prepareSetTempPacket(slowCoolingTransferValue);
                send_command(set_cmd);
            }
            else
            {
                send_command(get_cmd);
            }
        }

        private void connectTimerTick(object sender, EventArgs e)
        {
            connectTimer.Enabled = false;

            if (IsConnected == false)
            {
                if (port_combobox.SelectedItem == null)
                {
                    System.Windows.Forms.MessageBox.Show("Please select COM port.");
                    connect_button.Text = "Connect";
                    status_label.Text = "Disconnected";
                    this.Enabled = true;
                    return;
                }

                serialport = new SerialPort(port_combobox.SelectedItem.ToString(), baudrate, System.IO.Ports.Parity.None, 8, System.IO.Ports.StopBits.One);

                try
                {
                    //to prevent arduino nano reset - disable DTR pin
                    serialport.DtrEnable = false;

                    serialport.Open();
                    serialport.DiscardInBuffer();
                    serialport.DiscardOutBuffer();
                    serialport.ReadTimeout = 500;
                    serialport.WriteTimeout = 500;
                    serialport.DataReceived += new SerialDataReceivedEventHandler(read_packet);
                }
                catch
                {
                    System.Windows.Forms.MessageBox.Show("Cannot connect to selected COM port.");
                    connect_button.Text = "Connect";
                    status_label.Text = "Disconnected";
                    this.Enabled = true;
                    return;
                }

                const int maxAttempt = 5;
                int attempt;
                helloFlag = false;

                for (attempt = 0; attempt < maxAttempt; attempt++)
                {
                    try
                    {
                        send_command(info_cmd);
                        Thread.Sleep(1100);
                        if (helloFlag)
                        {
                            break;
                        }
                    }
                    catch
                    {
                        ;
                    }
                }

                if (attempt == maxAttempt)
                {
                    serialport.Close();
                    System.Windows.Forms.MessageBox.Show("Connection failed.");
                    connect_button.Text = "Connect";
                    status_label.Text = "Disconnected";
                    this.Enabled = true;
                    return;
                }

                status_label.Text = "Connected";
                connect_button.Text = "Disconnect";
                IsConnected = true;
                SaveSettings();

                get_timer.Enabled = true;
            }
            else
            {
                get_timer.Enabled = false;
                serialport.Close();
                connect_button.Text = "Connect";
                IsConnected = false;
                SaveSettings();
                status_label.Text = "Disconnected";
                this.Enabled = true;
            }
            port_combobox.Enabled = !IsConnected;
            control_groupbox.Enabled = IsConnected;
            slowCoolingNumericUpDown.Enabled = slowCoolingCheckBox.Checked;
        }

        private void setTimerTick(object sender, EventArgs e)
        {
            setTimer.Enabled = false;
            switch (currentCmd)
            {
                case 0x00:
                    {
                        send_command(set_cmd);
                        break;
                    }
                case 0x01:
                    {
                        send_command(pwr_cmd);
                        break;
                    }
                default:
                    {
                        break;
                    }
            }
            Thread.Sleep(1200);
            get_timer.Enabled = true;
        }

        private void connect_button_Click(object sender, EventArgs e)
        {
            if (!IsConnected)
            {
                connect_button.Text = "Connecting";
                status_label.Text = "Connecting...";
            }
            this.Enabled = false;
            waitPacket = PACKET_SKIP_GRAYOUT;
            connectTimer.Enabled = true;
        }

        private void prepareSetTempPacket(double temp)
        {
            short tmp;
            double set_temp = 0.0;

            set_temp = Math.Truncate(temp * 10) / 10;
            tmp = (short)((set_temp + temp_offset) * 10);

            set_cmd[1] = (byte)((tmp >> 8) & 0x00ff);
            set_cmd[2] = (byte)(tmp & 0x00ff);
            set_cmd[3] = crc8_block(set_cmd, 3);
        }

        private void set_button_Click(object sender, EventArgs e)
        {
            this.Enabled = false;
            waitPacket = PACKET_SKIP_GRAYOUT;
            if (slowCoolingCheckBox.Checked && pwm_state)
            {
                slowCoolingTimer.Enabled = false;
                slowCoolingTarger = (double)setTempNumericUpDown.Value;
                slowCoolingInterm = internal_temp;
                slowCoolingCoolingDirection = false;
                if ((internal_temp - slowCoolingTarger) > 0)
                {
                    slowCoolingCoolingDirection = true;
                }
                slowCoolingTimer.Enabled = true;
            }
            else
            {
                prepareSetTempPacket((double)setTempNumericUpDown.Value);
                currentCmd = SET_CMD;
                get_timer.Enabled = false;
                setTimer.Enabled = true;
            }
        }

        private void SaveSettings()
        {
            //save settings
            iniSettings pid_settings = new iniSettings();
            pid_settings.port = port_combobox.SelectedItem.ToString();
            pid_settings.set_temp = (double)setTempNumericUpDown.Value;
            pid_settings.slowCooling = slowCoolingCheckBox.Checked;
            pid_settings.slowCoolingSpeed = (double)slowCoolingNumericUpDown.Value;
            using (Stream writer = new FileStream(settings_filename, FileMode.Create))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(iniSettings));
                serializer.Serialize(writer, pid_settings);
            }
        }

        private byte crc8_block(byte[] pcBlock, byte len)
        {
            byte crc = 0xFF;
            byte i, j;

            for (j = 0; j < len; j++)
            {
                crc ^= pcBlock[j];
                for (i = 0; i < 8; i++)
                    if ((crc & 0x80) != 0) crc = (byte)((crc << 1) ^ 0x31);
                    else crc = (byte)(crc << 1);
            }
            return crc;
        }

        private void send_command(byte[] cmd)
        {
            try
            {
                serialport.Write(cmd, 0, cmd.Length);
            }
            catch
            {
                ;
            }
        }

        private void read_packet(object sender, EventArgs e)
        {
            byte crc, i;

            for (i = 0; i < buffer_size; i++)
                rx_buf[i] = 0;

            try
            {

                if ((serialport.BytesToRead != responce_packet_size) && (serialport.BytesToRead != info_packet_size))
                {
                    if (serialport.BytesToRead > ((responce_packet_size > info_packet_size) ? responce_packet_size : info_packet_size))
                    {
                        serialport.DiscardInBuffer();
                    }
                    return;
                }
                serialport.Read(rx_buf, 0, serialport.BytesToRead);
            }
            catch
            {
                ;
            }
            crc = crc8_block(rx_buf, (byte)(rx_buf.Length - 1));
            if (rx_buf[rx_buf.Length - 1] != crc)
            {
                return;
            }

            //check command responce v, d. renew interface if correct
            switch (rx_buf[0])
            {
                case 0x76:
                    {
                        if ((rx_buf[1] != HWREV) || (rx_buf[2] != SWREV) || (rx_buf[3] != SENSOR_CNT) || (rx_buf[4] != VAL_CNT))
                        {
                            return;
                        }
                        else
                        {
                            helloFlag = true;
                        }
                        break;
                    };
                case 0x64:
                    {
                        internal_temp = (((rx_buf[1] << 8) | (rx_buf[2])) - temp_offset * 10) / 10.0;
                        external_temp = (((rx_buf[3] << 8) | (rx_buf[4])) - temp_offset * 10) / 10.0;
                        val_temp = (((rx_buf[5] << 8) | (rx_buf[6])) - temp_offset * 10) / 10.0;

                        pwm = rx_buf[7];
                        err_code = rx_buf[8];
                        if (rx_buf[9] == 0x00)
                        {
                            pwm_state = false;
                        }
                        else
                        {
                            pwm_state = true;
                        }

                        if (pwm_state)
                        {
                            onoff_button.Text = "OFF TEC";
                        }
                        else
                        {
                            onoff_button.Text = "ON TEC";
                        }

                        if (waitPacket >= 0)
                        {
                            waitPacket--;
                        }
                        if ((!this.Enabled) && (waitPacket == 0))
                        {
                            this.Enabled = true;
                        }

                        switch (err_code)
                        {
                            case 0:
                                {
                                    error_label.Text = "No Errors";
                                    power_label.Text = (pwm * 100 / 255).ToString() + "%";
                                    externaltemp_label.Text = external_temp.ToString("F1") + "C";
                                    internaltemp_label.Text = internal_temp.ToString("F1") + "C";
                                    deltatemp_label.Text = (external_temp - internal_temp).ToString("F1") + "C";
                                    settemp_label.Text = val_temp.ToString("F1") + "C";
                                    if (pwm_state) status_label.Text = "On";
                                    else status_label.Text = "Off";
                                    break;
                                }
                            case 1:
                                {
                                    error_label.Text = "Internal sensor failed";
                                    power_label.Text = (pwm * 100 / 255).ToString() + "%";
                                    externaltemp_label.Text = external_temp.ToString("F1") + "C";
                                    internaltemp_label.Text = "N/A";
                                    deltatemp_label.Text = "N/A";
                                    settemp_label.Text = val_temp.ToString("F1") + "C";
                                    if (pwm_state) status_label.Text = "On";
                                    else status_label.Text = "Off";
                                    break;
                                }
                            case 2:
                                {
                                    error_label.Text = "External sensor failed";
                                    power_label.Text = (pwm * 100 / 255).ToString() + "%";
                                    externaltemp_label.Text = "N/A";
                                    internaltemp_label.Text = internal_temp.ToString("F1") + "C";
                                    deltatemp_label.Text = "N/A";
                                    settemp_label.Text = val_temp.ToString("F1") + "C";
                                    if (pwm_state) status_label.Text = "On";
                                    else status_label.Text = "Off";
                                    break;
                                }
                            case 3:
                                {
                                    error_label.Text = "Internal & External sensor failed";
                                    power_label.Text = (pwm * 100 / 255).ToString() + "%";
                                    externaltemp_label.Text = "N/A";
                                    internaltemp_label.Text = "N/A";
                                    deltatemp_label.Text = "N/A";
                                    settemp_label.Text = val_temp.ToString("F1") + "C";
                                    if (pwm_state) status_label.Text = "On";
                                    else status_label.Text = "Off";
                                    break;
                                }
                            default:
                                {
                                    error_label.Text = "Unknown Error";
                                    power_label.Text = (pwm * 100 / 255).ToString() + "%";
                                    externaltemp_label.Text = external_temp.ToString("F1") + "C";
                                    internaltemp_label.Text = internal_temp.ToString("F1") + "C";
                                    deltatemp_label.Text = (external_temp - internal_temp).ToString("F1") + "C";
                                    settemp_label.Text = val_temp.ToString("F1") + "C";
                                    if (pwm_state) status_label.Text = "On";
                                    else status_label.Text = "Off";
                                    break;
                                };
                        }

                        break;
                    };
                default:
                    {
                        return;
                    };
            }
            lastReceive = DateTime.Now;
        }

        private void onoff_button_Click(object sender, EventArgs e)
        {
            pwm_state = !pwm_state;
            waitPacket = PACKET_SKIP_GRAYOUT;

            pwr_cmd[1] = 0x00;
            if (pwm_state)
            {
                pwr_cmd[1] = 0x01;
            }
            else
            {
                slowCoolingTimer.Enabled = false;
            }
            pwr_cmd[2] = crc8_block(pwr_cmd, 2);

            currentCmd = PWR_CMD;
            this.Enabled = false;
            get_timer.Enabled = false;
            setTimer.Enabled = true;
        }

        private void slowCoolingTimerTick(Object source, ElapsedEventArgs e)
        {
            if (slowCoolingTimer.Enabled && slowCoolingCheckBox.Checked)
            {
                if (slowCoolingCoolingDirection)
                {
                    slowCoolingInterm -= ((double)slowCoolingNumericUpDown.Value);
                    if (slowCoolingInterm <= slowCoolingTarger)
                    {
                        slowCoolingInterm = slowCoolingTarger;
                    }
                }
                else
                {
                    slowCoolingInterm += ((double)slowCoolingNumericUpDown.Value);
                    if (slowCoolingInterm >= slowCoolingTarger)
                    {
                        slowCoolingInterm = slowCoolingTarger;
                    }
                }
                slowCoolingReady = true;
                slowCoolingTransferValue = slowCoolingInterm;

                if (System.Math.Abs(slowCoolingInterm - slowCoolingTarger) <= 0.01)
                {
                    slowCoolingTimer.Enabled = false;
                }
            }
        }

        private void slowCoolingCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            slowCoolingNumericUpDown.Enabled = slowCoolingCheckBox.Checked;
        }
    }

    public class iniSettings
    {
        public string port;
        public double set_temp;
        public bool slowCooling;
        public double slowCoolingSpeed;

        public iniSettings()
        {
            port = "COM1";
            set_temp = 50.0;
            slowCooling = false;
            slowCoolingSpeed = 0.1;
        }
    }
}
