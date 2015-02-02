namespace cooler_control_01
{
    partial class main_form
    {
        /// <summary>
        /// Требуется переменная конструктора.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Освободить все используемые ресурсы.
        /// </summary>
        /// <param name="disposing">истинно, если управляемый ресурс должен быть удален; иначе ложно.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Код, автоматически созданный конструктором форм Windows

        /// <summary>
        /// Обязательный метод для поддержки конструктора - не изменяйте
        /// содержимое данного метода при помощи редактора кода.
        /// </summary>
        private void InitializeComponent()
        {
            this.port_combobox = new System.Windows.Forms.ComboBox();
            this.connect_button = new System.Windows.Forms.Button();
            this.static_label2 = new System.Windows.Forms.Label();
            this.static_label1 = new System.Windows.Forms.Label();
            this.externaltemp_label = new System.Windows.Forms.Label();
            this.internaltemp_label = new System.Windows.Forms.Label();
            this.settemp_textbox = new System.Windows.Forms.TextBox();
            this.static_label7 = new System.Windows.Forms.Label();
            this.set_button = new System.Windows.Forms.Button();
            this.static_label4 = new System.Windows.Forms.Label();
            this.settemp_label = new System.Windows.Forms.Label();
            this.port_groupbox = new System.Windows.Forms.GroupBox();
            this.status_groupbox = new System.Windows.Forms.GroupBox();
            this.deltatemp_label = new System.Windows.Forms.Label();
            this.static_label3 = new System.Windows.Forms.Label();
            this.error_label = new System.Windows.Forms.Label();
            this.status_label = new System.Windows.Forms.Label();
            this.static_label6 = new System.Windows.Forms.Label();
            this.power_label = new System.Windows.Forms.Label();
            this.static_label5 = new System.Windows.Forms.Label();
            this.control_groupbox = new System.Windows.Forms.GroupBox();
            this.onoff_button = new System.Windows.Forms.Button();
            this.port_groupbox.SuspendLayout();
            this.status_groupbox.SuspendLayout();
            this.control_groupbox.SuspendLayout();
            this.SuspendLayout();
            // 
            // port_combobox
            // 
            this.port_combobox.FormattingEnabled = true;
            this.port_combobox.Items.AddRange(new object[] {
            "COM1",
            "COM2",
            "COM3",
            "COM4",
            "COM5",
            "COM6",
            "COM7",
            "COM8",
            "COM9",
            "COM10",
            "COM11",
            "COM12",
            "COM13",
            "COM14",
            "COM15",
            "COM16"});
            this.port_combobox.Location = new System.Drawing.Point(6, 19);
            this.port_combobox.Name = "port_combobox";
            this.port_combobox.Size = new System.Drawing.Size(70, 21);
            this.port_combobox.TabIndex = 1;
            // 
            // connect_button
            // 
            this.connect_button.Location = new System.Drawing.Point(83, 19);
            this.connect_button.Name = "connect_button";
            this.connect_button.Size = new System.Drawing.Size(69, 23);
            this.connect_button.TabIndex = 2;
            this.connect_button.Text = "Connect";
            this.connect_button.UseVisualStyleBackColor = true;
            this.connect_button.Click += new System.EventHandler(this.connect_button_Click);
            // 
            // static_label2
            // 
            this.static_label2.AutoSize = true;
            this.static_label2.Location = new System.Drawing.Point(6, 47);
            this.static_label2.Name = "static_label2";
            this.static_label2.Size = new System.Drawing.Size(55, 13);
            this.static_label2.TabIndex = 4;
            this.static_label2.Text = "External T";
            // 
            // static_label1
            // 
            this.static_label1.AutoSize = true;
            this.static_label1.Location = new System.Drawing.Point(6, 25);
            this.static_label1.Name = "static_label1";
            this.static_label1.Size = new System.Drawing.Size(52, 13);
            this.static_label1.TabIndex = 5;
            this.static_label1.Text = "Internal T";
            // 
            // externaltemp_label
            // 
            this.externaltemp_label.AutoSize = true;
            this.externaltemp_label.Location = new System.Drawing.Point(80, 47);
            this.externaltemp_label.Name = "externaltemp_label";
            this.externaltemp_label.Size = new System.Drawing.Size(29, 13);
            this.externaltemp_label.TabIndex = 6;
            this.externaltemp_label.Text = "0,0C";
            // 
            // internaltemp_label
            // 
            this.internaltemp_label.AutoSize = true;
            this.internaltemp_label.Location = new System.Drawing.Point(80, 25);
            this.internaltemp_label.Name = "internaltemp_label";
            this.internaltemp_label.Size = new System.Drawing.Size(29, 13);
            this.internaltemp_label.TabIndex = 7;
            this.internaltemp_label.Text = "0,0C";
            // 
            // settemp_textbox
            // 
            this.settemp_textbox.Enabled = false;
            this.settemp_textbox.Location = new System.Drawing.Point(86, 16);
            this.settemp_textbox.Name = "settemp_textbox";
            this.settemp_textbox.Size = new System.Drawing.Size(50, 20);
            this.settemp_textbox.TabIndex = 8;
            this.settemp_textbox.Text = "0,0";
            // 
            // static_label7
            // 
            this.static_label7.AutoSize = true;
            this.static_label7.Location = new System.Drawing.Point(7, 21);
            this.static_label7.Name = "static_label7";
            this.static_label7.Size = new System.Drawing.Size(33, 13);
            this.static_label7.TabIndex = 9;
            this.static_label7.Text = "Set T";
            // 
            // set_button
            // 
            this.set_button.Enabled = false;
            this.set_button.Location = new System.Drawing.Point(10, 40);
            this.set_button.Name = "set_button";
            this.set_button.Size = new System.Drawing.Size(69, 23);
            this.set_button.TabIndex = 10;
            this.set_button.Text = "Set";
            this.set_button.UseVisualStyleBackColor = true;
            this.set_button.Click += new System.EventHandler(this.set_button_Click);
            // 
            // static_label4
            // 
            this.static_label4.AutoSize = true;
            this.static_label4.Location = new System.Drawing.Point(7, 92);
            this.static_label4.Name = "static_label4";
            this.static_label4.Size = new System.Drawing.Size(33, 13);
            this.static_label4.TabIndex = 11;
            this.static_label4.Text = "Set T";
            // 
            // settemp_label
            // 
            this.settemp_label.AutoSize = true;
            this.settemp_label.Location = new System.Drawing.Point(80, 92);
            this.settemp_label.Name = "settemp_label";
            this.settemp_label.Size = new System.Drawing.Size(29, 13);
            this.settemp_label.TabIndex = 12;
            this.settemp_label.Text = "0,0C";
            // 
            // port_groupbox
            // 
            this.port_groupbox.Controls.Add(this.port_combobox);
            this.port_groupbox.Controls.Add(this.connect_button);
            this.port_groupbox.Location = new System.Drawing.Point(16, 8);
            this.port_groupbox.Name = "port_groupbox";
            this.port_groupbox.Size = new System.Drawing.Size(164, 57);
            this.port_groupbox.TabIndex = 13;
            this.port_groupbox.TabStop = false;
            this.port_groupbox.Text = "Port";
            // 
            // status_groupbox
            // 
            this.status_groupbox.Controls.Add(this.deltatemp_label);
            this.status_groupbox.Controls.Add(this.static_label3);
            this.status_groupbox.Controls.Add(this.settemp_label);
            this.status_groupbox.Controls.Add(this.static_label4);
            this.status_groupbox.Controls.Add(this.error_label);
            this.status_groupbox.Controls.Add(this.status_label);
            this.status_groupbox.Controls.Add(this.static_label6);
            this.status_groupbox.Controls.Add(this.static_label2);
            this.status_groupbox.Controls.Add(this.power_label);
            this.status_groupbox.Controls.Add(this.static_label1);
            this.status_groupbox.Controls.Add(this.static_label5);
            this.status_groupbox.Controls.Add(this.externaltemp_label);
            this.status_groupbox.Controls.Add(this.internaltemp_label);
            this.status_groupbox.Location = new System.Drawing.Point(16, 71);
            this.status_groupbox.Name = "status_groupbox";
            this.status_groupbox.Size = new System.Drawing.Size(164, 186);
            this.status_groupbox.TabIndex = 14;
            this.status_groupbox.TabStop = false;
            this.status_groupbox.Text = "Status";
            // 
            // deltatemp_label
            // 
            this.deltatemp_label.AutoSize = true;
            this.deltatemp_label.Location = new System.Drawing.Point(80, 69);
            this.deltatemp_label.Name = "deltatemp_label";
            this.deltatemp_label.Size = new System.Drawing.Size(29, 13);
            this.deltatemp_label.TabIndex = 19;
            this.deltatemp_label.Text = "0,0C";
            // 
            // static_label3
            // 
            this.static_label3.AutoSize = true;
            this.static_label3.Location = new System.Drawing.Point(6, 69);
            this.static_label3.Name = "static_label3";
            this.static_label3.Size = new System.Drawing.Size(42, 13);
            this.static_label3.TabIndex = 18;
            this.static_label3.Text = "Delta T";
            // 
            // error_label
            // 
            this.error_label.AutoSize = true;
            this.error_label.Location = new System.Drawing.Point(7, 163);
            this.error_label.Name = "error_label";
            this.error_label.Size = new System.Drawing.Size(0, 13);
            this.error_label.TabIndex = 17;
            // 
            // status_label
            // 
            this.status_label.AutoSize = true;
            this.status_label.Location = new System.Drawing.Point(80, 141);
            this.status_label.Name = "status_label";
            this.status_label.Size = new System.Drawing.Size(73, 13);
            this.status_label.TabIndex = 16;
            this.status_label.Text = "Disconnected";
            // 
            // static_label6
            // 
            this.static_label6.AutoSize = true;
            this.static_label6.Location = new System.Drawing.Point(7, 141);
            this.static_label6.Name = "static_label6";
            this.static_label6.Size = new System.Drawing.Size(37, 13);
            this.static_label6.TabIndex = 15;
            this.static_label6.Text = "Status";
            // 
            // power_label
            // 
            this.power_label.AutoSize = true;
            this.power_label.Location = new System.Drawing.Point(80, 117);
            this.power_label.Name = "power_label";
            this.power_label.Size = new System.Drawing.Size(21, 13);
            this.power_label.TabIndex = 14;
            this.power_label.Text = "0%";
            this.power_label.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // static_label5
            // 
            this.static_label5.AutoSize = true;
            this.static_label5.Location = new System.Drawing.Point(7, 116);
            this.static_label5.Name = "static_label5";
            this.static_label5.Size = new System.Drawing.Size(69, 13);
            this.static_label5.TabIndex = 13;
            this.static_label5.Text = "Cooler power";
            // 
            // control_groupbox
            // 
            this.control_groupbox.Controls.Add(this.onoff_button);
            this.control_groupbox.Controls.Add(this.static_label7);
            this.control_groupbox.Controls.Add(this.set_button);
            this.control_groupbox.Controls.Add(this.settemp_textbox);
            this.control_groupbox.Location = new System.Drawing.Point(16, 263);
            this.control_groupbox.Name = "control_groupbox";
            this.control_groupbox.Size = new System.Drawing.Size(164, 74);
            this.control_groupbox.TabIndex = 15;
            this.control_groupbox.TabStop = false;
            this.control_groupbox.Text = "Control";
            // 
            // onoff_button
            // 
            this.onoff_button.Enabled = false;
            this.onoff_button.Location = new System.Drawing.Point(85, 40);
            this.onoff_button.Name = "onoff_button";
            this.onoff_button.Size = new System.Drawing.Size(69, 23);
            this.onoff_button.TabIndex = 11;
            this.onoff_button.Text = "Cooler on";
            this.onoff_button.UseVisualStyleBackColor = true;
            this.onoff_button.Click += new System.EventHandler(this.onoff_button_Click);
            // 
            // main_form
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(195, 355);
            this.Controls.Add(this.control_groupbox);
            this.Controls.Add(this.status_groupbox);
            this.Controls.Add(this.port_groupbox);
            this.MaximumSize = new System.Drawing.Size(203, 384);
            this.MinimumSize = new System.Drawing.Size(203, 384);
            this.Name = "main_form";
            this.Text = "Cooler control v.0.1";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FormIsClosing);
            this.port_groupbox.ResumeLayout(false);
            this.status_groupbox.ResumeLayout(false);
            this.status_groupbox.PerformLayout();
            this.control_groupbox.ResumeLayout(false);
            this.control_groupbox.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ComboBox port_combobox;
        private System.Windows.Forms.Button connect_button;
        private System.Windows.Forms.Label static_label2;
        private System.Windows.Forms.Label static_label1;
        private System.Windows.Forms.Label externaltemp_label;
        private System.Windows.Forms.Label internaltemp_label;
        private System.Windows.Forms.TextBox settemp_textbox;
        private System.Windows.Forms.Label static_label7;
        private System.Windows.Forms.Button set_button;
        private System.Windows.Forms.Label static_label4;
        private System.Windows.Forms.Label settemp_label;
        private System.Windows.Forms.GroupBox port_groupbox;
        private System.Windows.Forms.GroupBox status_groupbox;
        private System.Windows.Forms.GroupBox control_groupbox;
        private System.Windows.Forms.Label static_label5;
        private System.Windows.Forms.Label power_label;
        private System.Windows.Forms.Label status_label;
        private System.Windows.Forms.Label static_label6;
        private System.Windows.Forms.Label static_label3;
        private System.Windows.Forms.Label error_label;
        private System.Windows.Forms.Label deltatemp_label;
        private System.Windows.Forms.Button onoff_button;
    }
}

