using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.IO;                // work with files
using System.IO.Ports;          // work with COM ports
using System.Xml.Serialization; // class savings

namespace TEC_control_01
{
    public partial class main_form : Form
    {
        private SerialPort serialport;

        private int pwm = 0, err_code = 0, pwm_state = 0;
        private double external_temp = 0.0, internal_temp = 0.0, val_temp = 0.0;
        private double set_temp = 0.0;

        private bool IsConnected = false;

        private byte[] info_cmd = { 0x69, 0x9f };
        private byte[] get_cmd = { 0x67, 0x80 };
        private byte[] set_cmd = { 0x73, 0x00, 0x00, 0x00 };
        private byte[] pwm_cmd = { 0x70, 0x00, 0x00 };

        private const int baudrate = 9600;

        private const string settings_filename = "tec_control_settings.xml";

        private const byte buffer_size = 11;
        private const byte responce_packet_size = 11;
        private const byte info_packet_size = 6;

        private const byte HWREV = 0x01;
        private const byte SWREV = 0x01;
        private const byte SENSOR_CNT = 0x02;
        private const byte VAL_CNT = 0x01;

        private const int low_set_temp = -50;
        private const int high_set_temp = 50;

        private const int temp_offset = 128;

        private const byte _ON = 0x01;
        private const byte _OFF = 0x00;

        private static byte timer_state = 0;
        private System.Windows.Forms.Timer get_timer = null;

        byte[] rx_buf;

        public main_form()
        {
            InitializeComponent();

            rx_buf = new byte[buffer_size];

            get_timer = new System.Windows.Forms.Timer();
            get_timer.Interval = 1000;
            get_timer.Tick += new EventHandler(timer_Tick);
            get_timer.Enabled = false;

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
                        if ((pid_settings.set_temp < low_set_temp) || (pid_settings.set_temp > high_set_temp)) pid_settings.set_temp = 0;
                        settemp_textbox.Text = pid_settings.set_temp.ToString("F1");
                        set_temp = pid_settings.set_temp;

                        string[] comPorts;
                        comPorts = SerialPort.GetPortNames();
                        int j;
                        for (j = 0; j < comPorts.Length; j++)
                        {
                            port_combobox.Items.Add(comPorts[j]);
                            if (comPorts[j] == pid_settings.port) port_combobox.SelectedIndex = j;
                        }
                    }
                }
                catch
                {
                    System.Windows.Forms.MessageBox.Show(settings_filename + " damaged, use settings by default.");
                    port_combobox.SelectedIndex = 0;
                    settemp_textbox.Text = "0,0";
                    set_temp = 0.0;
                }
            }
            else
            {
                string[] comPorts;
                comPorts = SerialPort.GetPortNames();
                int j;
                for (j = 0; j < comPorts.Length; j++)                
                    port_combobox.Items.Add(comPorts[j]);                   
            }
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            if (timer_state == 0)
            {
                send_command(get_cmd);
                timer_state = 1;
            }
            else
            {
                if (read_packet() == 0) serialport.DiscardInBuffer();
                timer_state = 0;
            }
        }

        private void connect_button_Click(object sender, EventArgs e)
        {
            if (IsConnected == false)
            {
                if (port_combobox.SelectedItem == null)
                {
                    System.Windows.Forms.MessageBox.Show("Please select COM port.");
                    return;
                }

                serialport = new SerialPort(port_combobox.SelectedItem.ToString(), baudrate, System.IO.Ports.Parity.None, 8, System.IO.Ports.StopBits.One);

                try
                {
                    serialport.Open();
                    serialport.DiscardInBuffer();
                    serialport.DiscardOutBuffer();
                    serialport.ReadTimeout = 500;
                    serialport.WriteTimeout = 500;
                }
                catch
                {
                    System.Windows.Forms.MessageBox.Show("Cannot connect to selected COM port.");
                    return;
                }

                send_command(info_cmd);
                Thread.Sleep(1000);
                if (read_packet() == 0)
                {
                    System.Windows.Forms.MessageBox.Show("Connection failed.");
                    serialport.Close();
                    return;
                }

                status_label.Text = "Connected";
                set_button.Enabled = true;
                settemp_textbox.Enabled = true;
                onoff_button.Enabled = false;
                connect_button.Text = "Disconnect";
                IsConnected = true;
                SaveSettings();

                get_timer.Enabled = true;
            }
            else
            {
                get_timer.Enabled = false;
                serialport.Close();
                set_button.Enabled = false;
                settemp_textbox.Enabled = false;
                onoff_button.Enabled = false;
                connect_button.Text = "Connect";
                IsConnected = false;
                status_label.Text = "Disconnected";
            }
            Thread.Sleep(1000);
        }

        private void set_button_Click(object sender, EventArgs e)
        {
            short tmp;

            Thread.Sleep(1000);
            try
            {
                //.Replace(',', '.')
                set_temp = Convert.ToDouble(settemp_textbox.Text);
            }
            catch
            {
                System.Windows.Forms.MessageBox.Show("Wrong desired temperature.");
                return;
            }
            if ((set_temp < low_set_temp) || (set_temp > high_set_temp))
            {
                System.Windows.Forms.MessageBox.Show("Too large/small desired temperature. Correct range is [" + low_set_temp.ToString() + ";" + high_set_temp.ToString() + "]");
                return;
            }

            set_temp = Math.Truncate(set_temp * 10) / 10;
            tmp = (short)((set_temp + temp_offset) * 10);

            set_cmd[1] = (byte)((tmp >> 8) & 0x00ff);
            set_cmd[2] = (byte)(tmp & 0x00ff);
            set_cmd[3] = crc8_block(set_cmd, 3);

            send_command(set_cmd);
            Thread.Sleep(1000);
            SaveSettings();
        }

        private void FormIsClosing(object sender, FormClosingEventArgs e)
        {
            if (IsConnected) serialport.Close();
        }

        private void SaveSettings()
        {
            //save settings
            iniSettings pid_settings = new iniSettings();
            pid_settings.port = port_combobox.SelectedItem.ToString();
            pid_settings.set_temp = set_temp;
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
            serialport.Write(cmd, 0, cmd.Length);
        }

        private byte read_packet()
        {
            byte crc, i;

            for (i = 0; i < buffer_size; i++)
                rx_buf[i] = 0;

            if ((serialport.BytesToRead != responce_packet_size) && (serialport.BytesToRead != info_packet_size)) return 0;
            serialport.Read(rx_buf, 0, serialport.BytesToRead);
            crc = crc8_block(rx_buf, (byte)(rx_buf.Length - 1));
            if (rx_buf[rx_buf.Length - 1] != crc) return 0;

            //check command responce v, d. renew interface if correct
            switch (rx_buf[0])
            {
                case 0x76:
                    {
                        if ((rx_buf[1] != HWREV) || (rx_buf[2] != SWREV) || (rx_buf[3] != SENSOR_CNT) || (rx_buf[4] != VAL_CNT)) return 0;
                        break;
                    };
                case 0x64:
                    {
                        internal_temp = (((rx_buf[1] << 8) | (rx_buf[2])) - temp_offset * 10) / 10.0;
                        external_temp = (((rx_buf[3] << 8) | (rx_buf[4])) - temp_offset * 10) / 10.0;
                        val_temp = (((rx_buf[5] << 8) | (rx_buf[6])) - temp_offset * 10) / 10.0;

                        pwm = rx_buf[7];
                        err_code = rx_buf[8];
                        pwm_state = rx_buf[9];

                        if (pwm_state == 0x00)
                        {
                            onoff_button.Text = "ON TEC";
                            onoff_button.Enabled = true;
                        }
                        else
                        {
                            onoff_button.Text = "OFF TEC";
                            onoff_button.Enabled = true;
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
                                    if (pwm_state == 0) status_label.Text = "Off";
                                    else status_label.Text = "On";
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
                                    if (pwm_state == 0) status_label.Text = "Off";
                                    else status_label.Text = "On";
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
                                    if (pwm_state == 0) status_label.Text = "Off";
                                    else status_label.Text = "On";
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
                                    if (pwm_state == 0) status_label.Text = "Off";
                                    else status_label.Text = "On";
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
                                    if (pwm_state == 0) status_label.Text = "Off";
                                    else status_label.Text = "On";
                                    break;
                                };
                        }

                        break;
                    };
                default:
                    {
                        return 0;
                    };
            }
            return 1;
        }

        private void onoff_button_Click(object sender, EventArgs e)
        {
            if (pwm_state == 0)
            {
                pwm_state = 1;
                onoff_button.Text = "ON TEC";
            }
            else
            {
                pwm_state = 0;
                onoff_button.Text = "OFF TEC";
            }

            onoff_button.Enabled = false;

            pwm_cmd[1] = (byte)pwm_state;
            pwm_cmd[2] = crc8_block(pwm_cmd, 2);

            Thread.Sleep(1000);
            send_command(pwm_cmd);
            Thread.Sleep(1000);
        }
    }

    public class iniSettings
    {
        public string port;
        public double set_temp;

        public iniSettings()
        {
            port = "COM1";
            set_temp = 0.0;
        }
    }
}
