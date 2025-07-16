using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Ports;
using System.Threading;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using System.Collections;
using System.IO;
using System.Timers;
using System.Reflection.Emit;
using System.Drawing.Imaging;

namespace JTGB_UF_7609_Config_Software
{
    public partial class Form1 : Form
    {
        private bool enterPressed = false; // 标志：是否按下回车键
        private byte sensorID = 0;
        private byte readBackFlag = 0;
        private int packetCount = 0; // 记录数据包数量
        private StreamWriter csvWriter;
        private string csvFilePath;
        private bool isFirstWrite = true; // 标记是否首次写入（用于添加标题）
        private int autoRecordFlag = 0; // 记录数据点数量
        private int autoAskFlag = 0;    // 记录数据点数量
        private bool isButton1Turn = true; // 用于跟踪当前执行哪个按钮
        private SerialPort serialPort;
        public Form1()
        {
            InitializeComponent();
            InitializeSerialPorts();
            serialPort = new SerialPort();
            button3.Enabled = false;
            // 初始化定时器
            timer1.Interval = 1000; // 每1秒触发一次
            timer1.Tick += Timer_Tick; // 绑定事件处理函数
            timer1.Start(); // 启动定时器
        }


        private void InitializeSerialPorts()
        {
            // 获取可用的串口名称数组
            string[] portNames = SerialPort.GetPortNames();
            comboBoxSerialPorts.Items.Clear();

            // 将串口名称添加到 ComboBox 中
            foreach (string portName in portNames)
            {
                comboBoxSerialPorts.Items.Add(portName);
            }

            // 如果有可用串口，选择第一个串口
            if (comboBoxSerialPorts.Items.Count > 0)
            {
                comboBoxSerialPorts.SelectedIndex = 0;
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {

            // 获取选定的串口名称
            string selectedPort = comboBoxSerialPorts.SelectedItem.ToString();

            try
            {
                // 设置串口的属性
                serialPort.PortName = selectedPort;
                serialPort.BaudRate = 9600;  // 设置波特率，根据实际需要修改
                serialPort.DataBits = 8;
                serialPort.StopBits = StopBits.One;
                serialPort.Parity = Parity.None;

                // 打开串口连接
                serialPort.Open();

                button2.Enabled = false;
                button3.Enabled = true;
                comboBoxSerialPorts.Enabled = false;

                // 在此处添加你的串口通信代码

                // 例如，发送数据
                // serialPort.Write("Hello, Serial Port!");

                // 或者监听数据
                // serialPort.DataReceived += SerialPort_DataReceived;

            }
            catch (Exception ex)
            {
                // 处理异常，例如显示错误消息
                MessageBox.Show("Error: " + ex.Message);
            }


        }
        private void ReadSerialData()
        {
            int intValue = 0; // 用于存储接收到的整数值

            if (serialPort.IsOpen)
            {
                // 获取串口接收的字节数
                int byteCount = serialPort.BytesToRead;

                if (byteCount > 0)
                {
                    byte[] buffer = new byte[byteCount];
                    serialPort.Read(buffer, 0, byteCount);  // 读取数据

                    // 将字节数组转换为16进制字符串
                    string hexData = BitConverter.ToString(buffer).Replace("-", " ");  // 以空格分隔
                    ushort crcReceived = BitConverter.ToUInt16(buffer, byteCount - 2);
                    ushort crc16val = CalculateCrc(buffer, byteCount - 2);
                    // 比较计算的 CRC 和接收到的 CRC
                    if (crc16val == crcReceived)
                    {
                        readBackFlag = 1;
                        Console.WriteLine("CRC 校验通过！");
                        // 如果是在 UI 线程以外的线程中，需要使用 Invoke 来更新 TextBox
                        if (textBox2.InvokeRequired)
                        {
                            // 使用 Invoke 方法在 UI 线程上执行更新操作
                            textBox2.Invoke(new Action(() =>
                            {
                                textBox2.Text = hexData;  // 更新 TextBox 中的文本
                            }));
                        }
                        else
                        {
                            // 如果已经在 UI 线程，可以直接更新
                            textBox2.Text = hexData;
                        }
                        if (autoRecordFlag == 1)
                        {
                            packetCount++;
                            // 在UI线程更新label2（防止跨线程访问异常）
                            this.Invoke((MethodInvoker)delegate
                            {
                                label12.Text = packetCount.ToString();
                            });
                        }

                        // 更新 UI 状态
                        Invoke((MethodInvoker)(() =>
                        {

                            if (buffer[0] == sensorID)
                            {
                                Byte[] ramData = new Byte[4];
                                ramData[3] = buffer[3]; ramData[2] = buffer[4]; ramData[1] = buffer[5]; ramData[0] = buffer[6];
                                float temp = BitConverter.ToSingle(ramData, 0);
                                if (temp < 1000)
                                {
                                    textBox1.Text = temp.ToString("f3");
                                    label1.Text = "uSv/h";
                                }
                                else
                                {
                                    temp = temp / 1000;
                                    textBox1.Text = temp.ToString("f3");
                                    label1.Text = "mSv/h";
                                }
                                ramData[3] = buffer[7]; ramData[2] = buffer[8]; ramData[1] = buffer[9]; ramData[0] = buffer[10];
                                temp = BitConverter.ToSingle(ramData, 0);
                                textBox4.Text = temp.ToString("f3");//低报警阈值

                                ramData[3] = buffer[11]; ramData[2] = buffer[12]; ramData[1] = buffer[13]; ramData[0] = buffer[14];
                                temp = BitConverter.ToSingle(ramData, 0);
                                textBox15.Text = temp.ToString("f0");//高报警阈值

                                ramData[3] = buffer[15]; ramData[2] = buffer[16]; ramData[1] = buffer[17]; ramData[0] = buffer[18];
                                temp = BitConverter.ToUInt32(ramData, 0);
                                textBox5.Text = temp.ToString("f0"); //cps1

                                ramData[3] = buffer[19]; ramData[2] = buffer[20]; ramData[1] = buffer[21]; ramData[0] = buffer[22];
                                temp = BitConverter.ToUInt32(ramData, 0);
                                textBox6.Text = temp.ToString("f0"); //cps2

                                ramData[3] = buffer[23]; ramData[2] = buffer[24]; ramData[1] = buffer[25]; ramData[0] = buffer[26];
                                temp = BitConverter.ToSingle(ramData, 0);
                                textBox13.Text = temp.ToString("f2");//探测器状态

                                ramData[3] = buffer[27]; ramData[2] = buffer[28]; ramData[1] = buffer[29]; ramData[0] = buffer[30];
                                temp = BitConverter.ToSingle(ramData, 0);
                                textBox7.Text = temp.ToString("f2");//温度

                                ramData[3] = buffer[31]; ramData[2] = buffer[32]; ramData[1] = buffer[33]; ramData[0] = buffer[34];
                                temp = BitConverter.ToSingle(ramData, 0);
                                textBox8.Text = temp.ToString("f5");//低灵敏度

                                ramData[3] = buffer[35]; ramData[2] = buffer[36]; ramData[1] = buffer[37]; ramData[0] = buffer[38];
                                temp = BitConverter.ToSingle(ramData, 0);
                                textBox9.Text = temp.ToString("f5");//高灵敏度

                                ramData[3] = buffer[39]; ramData[2] = buffer[40]; ramData[1] = buffer[41]; ramData[0] = buffer[42];
                                temp = BitConverter.ToSingle(ramData, 0);
                                textBox10.Text = temp.ToString("f5");//低死时间

                                ramData[3] = buffer[43]; ramData[2] = buffer[44]; ramData[1] = buffer[45]; ramData[0] = buffer[46];
                                temp = BitConverter.ToSingle(ramData, 0);
                                textBox11.Text = temp.ToString("f5");//高死时间

                                ramData[3] = buffer[47]; ramData[2] = buffer[48]; ramData[1] = buffer[49]; ramData[0] = buffer[50];
                                temp = BitConverter.ToSingle(ramData, 0);
                                textBox12.Text = temp.ToString("f0");//本底

                                ramData[3] = buffer[23]; ramData[2] = buffer[24]; ramData[1] = buffer[25]; ramData[0] = buffer[26];
                                temp = BitConverter.ToUInt32(ramData, 0);
                                textBox14.Text = temp.ToString("f0"); //info
                            }


                        }));

                        if (csvWriter != null)
                        {
                            this.Invoke((MethodInvoker)delegate { WriteToCsv(buffer[0], intValue); });
                        }
                    }
                    else
                    {
                        Console.WriteLine("CRC 校验失败！");
                    }
                    // 打印16进制数据
                    Console.WriteLine($"接收到的数据（16进制）: {hexData}");
                }
                else
                {
                    Console.WriteLine("没有接收到数据。");
                }
            }
            else
            {
                Console.WriteLine("串口未打开！");
            }
        }

        private void WriteToCsv(byte addr, int value)
        {
            try
            {
                if (isFirstWrite)
                {
                    // 写入标题
                    string header = "年月日,时分秒,地址,实时剂量率,cps1,cps2,温度,";
                    csvWriter.WriteLine(header);
                    isFirstWrite = false;
                }

                // 写入时间戳和数据
                string date = DateTime.Now.ToString("yyyy-MM-dd");
                string time = DateTime.Now.ToString("HH:mm:ss");
                string dataLine = $"{date},{time},{string.Join(",", addr.ToString(), textBox1.Text+ label1.Text, textBox5.Text, textBox6.Text, textBox7.Text)}";
                csvWriter.WriteLine(dataLine);
                csvWriter.Flush();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"写入 CSV 失败: {ex.Message}");
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            // 关闭窗体时，确保串口已经关闭
            if (serialPort.IsOpen)
            {
                serialPort.Close();
                button3.Enabled = false;
                button2.Enabled = true;
                comboBoxSerialPorts.Enabled = true;
            }
        }

        private void SendData(ushort value)
        {
            byte[] byteArray = new byte[8];

            if (serialPort.IsOpen)
            {
                byteArray[0] = (byte)value;
                byteArray[1] = 0x03;
                byteArray[2] = 0x00;
                byteArray[3] = 0x00;
                byteArray[4] = 0x00;
                byteArray[5] = 0x1D;
                ushort crc16val = CalculateCrc(byteArray, 6);
                byteArray[6] = (byte)(crc16val & 0xFF);
                byteArray[7] = (byte)(crc16val >> 8);
                serialPort.Write(byteArray, 0, byteArray.Length);
            }
        }

        private void SendNewDetectorAddrSet(byte addr)
        {
            byte[] byteArray = new byte[8];
            if (serialPort.IsOpen)
            {
                byteArray[0] = (byte)(comboBox1.SelectedIndex+1);
                byteArray[1] = 0x06;
                byteArray[2] = 0x00;
                byteArray[3] = 0x00;
                byteArray[4] = 0x41;
                byteArray[5] = addr;
                ushort crc16val = CalculateCrc(byteArray, 6);
                byteArray[6] = (byte)(crc16val & 0xFF);
                byteArray[7] = (byte)(crc16val >> 8);
                serialPort.Write(byteArray, 0, byteArray.Length);
            }
        }

        private void SendOldDetectorAddrSet(byte addr)
        {
            byte[] byteArray = new byte[8];
            if (serialPort.IsOpen)
            {
                byteArray[0] = (byte)(comboBox1.SelectedIndex + 1);
                byteArray[1] = 0x06;
                byteArray[2] = 0x00;
                byteArray[3] = 0x00;
                byteArray[4] = 0x56;
                byteArray[5] = addr;
                ushort crc16val = CalculateCrc(byteArray, 6);
                byteArray[6] = (byte)(crc16val & 0xFF);
                byteArray[7] = (byte)(crc16val >> 8);
                serialPort.Write(byteArray, 0, byteArray.Length);
            }
        }


        // 计算CRC16
        // CRC16 Modbus RTU 计算，带长度参数
        public static ushort CalculateCrc(byte[] data, int length)
        {
            ushort crc = 0xFFFF;  // CRC初始值
            for (int i = 0; i < length; i++)  // 只计算前 `length` 个字节
            {
                byte byteData = data[i];
                crc ^= byteData;  // 将当前字节与CRC值进行异或
                for (int j = 8; j > 0; j--)  // 每个字节做8次移位
                {
                    if ((crc & 0x0001) != 0)
                    {
                        crc >>= 1;  // 右移1位
                        crc ^= 0xA001;  // XOR 0xA001多项式
                    }
                    else
                    {
                        crc >>= 1;  // 右移1位
                    }
                }
            }
            return crc;
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            autoAskFlag = 0;
            textBox2.Clear();
            if (serialPort.IsOpen)
            {
                sensorID = (byte)(comboBox1.SelectedIndex + 1);
                SendData((ushort)(comboBox1.SelectedIndex + 1));
                // 异步等待100毫秒
                await Task.Delay(100);
                // 读取串口回传数据
                ReadSerialData();
                button7.Enabled = true;
                comboBox1.Enabled = true;
                comboBox2.Enabled = true;
                comboBox3.Enabled = true;
            }
            else
            {
                MessageBox.Show("请打开串口");
            }
        }





        private void Form1_Load(object sender, EventArgs e)
        {
            textBox1.ReadOnly = true;
            textBox2.ReadOnly = true;
            textBox1.Select(0, 0); // 取消 TextBox 的文本选择
            textBox2.Select(0, 0); // 取消 TextBox 的文本选择
            textBox1.TabStop = false; // 禁止通过 Tab 键聚焦
            textBox2.TabStop = false; // 禁止通过 Tab 键聚焦
            this.Focus(); // 将焦点设置到窗体本身
            this.FormBorderStyle = FormBorderStyle.FixedSingle; // 固定边框，无法调整大小
            this.MaximizeBox = false; // 禁用最大化按钮
            this.MinimizeBox = true;  // 可选：保留最小化按钮
            label3.Text = "";
            button6.Enabled = true;
            button5.Enabled = false;
            autoRecordFlag = 0;
            autoAskFlag = 0;
            button1.Enabled = true;
            // 初始化 comboBox1，添加 1 到 10
            for (int i = 1; i <= 10; i++)
            {
                comboBox1.Items.Add(i);
                comboBox2.Items.Add(i);
                comboBox3.Items.Add(i);
            }
            // 可选：设置默认选中项（例如第一个值：1）
            comboBox1.SelectedIndex = 0;

            // 新增 KeyDown 事件
            textBox3.KeyDown += TextBox3_KeyDown;
            textBox16.KeyDown += TextBox16_KeyDown;
            textBox18.KeyDown += TextBox18_KeyDown;
        }

        private void TextBox3_KeyDown(object sender, KeyEventArgs e)
        {
            // 按下回车键时设置标志
            if (e.KeyCode == Keys.Enter)
            {
                enterPressed = true;
                e.SuppressKeyPress = true; // 防止回车键触发其他行为
                processingtextbox3();
            }
        }
        private void TextBox16_KeyDown(object sender, KeyEventArgs e)
        {
            // 按下回车键时设置标志
            if (e.KeyCode == Keys.Enter)
            {
                enterPressed = true;
                e.SuppressKeyPress = true; // 防止回车键触发其他行为
                processingtextbox16();
            }
        }
        private void TextBox18_KeyDown(object sender, KeyEventArgs e)
        {
            // 按下回车键时设置标志
            if (e.KeyCode == Keys.Enter)
            {
                enterPressed = true;
                e.SuppressKeyPress = true; // 防止回车键触发其他行为
                processingtextbox18();
            }
        }


        private async void Timer_Tick(object sender, EventArgs e)
        {
            if (autoAskFlag == 1 && serialPort.IsOpen)
            {
                textBox2.Clear();
                SendData((ushort)(comboBox1.SelectedIndex+1));
                // 异步等待100毫秒
                await Task.Delay(100);
                // 读取串口回传数据
                ReadSerialData();
            }
        }

        private void button6_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "CSV 文件 (*.csv)|*.csv";
                saveFileDialog.DefaultExt = "csv";
                saveFileDialog.AddExtension = true;

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    csvFilePath = saveFileDialog.FileName;
                    // 使用 UTF-8 with BOM 编码
                    csvWriter = new StreamWriter(csvFilePath, false, new UTF8Encoding(true)); // false 表示覆盖模式，true 表示带 BOM
                    isFirstWrite = true; // 重置首次写入标志
                    label3.Text = "CSV file opened: " + csvFilePath;
                    button6.Enabled = false;
                    button5.Enabled = true;
                    autoRecordFlag = 1;
                    button1.Enabled = true;
                    packetCount = 0;
                    // 在UI线程更新label2（防止跨线程访问异常）
                    this.Invoke((MethodInvoker)delegate
                    {
                        label12.Text = packetCount.ToString();
                    });
                }
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            button6.Enabled = true;
            button5.Enabled = false;
            if (csvWriter != null)
            {
                try
                {
                    csvWriter.Close();
                    csvWriter.Dispose();
                    csvWriter = null;
                    label3.Text = "CSV file closed";
                    button1.Enabled = true;
                    button1.Enabled = true;
                    autoRecordFlag = 0;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"关闭 CSV 失败: {ex.Message}");
                }
            }
        }


        private async void newDector_Click(object sender, EventArgs e)
        {
            if (serialPort.IsOpen)
            {              
                SendNewDetectorAddrSet((byte)(comboBox2.SelectedIndex + 1));
                // 异步等待100毫秒
                await Task.Delay(100);
                // 读取串口回传数据
                SendData((ushort)(comboBox2.SelectedIndex + 1));
                sensorID = (byte)(comboBox2.SelectedIndex + 1);
                readBackFlag = 0;

                // 异步等待100毫秒
                await Task.Delay(100);
                // 读取串口回传数据
                ReadSerialData();

                if (readBackFlag > 0)
                {
                    MessageBox.Show("地址修改成功");
                    comboBox1.SelectedIndex = comboBox2.SelectedIndex;
                }
                else
                {
                    MessageBox.Show("地址修改失败");
                }
            }
        }
        private async void oldDector_Click(object sender, EventArgs e)
        {
            if (serialPort.IsOpen)
            {
 
                SendOldDetectorAddrSet((byte)(comboBox3.SelectedIndex + 1));
                // 异步等待100毫秒
                await Task.Delay(100);
                // 读取串口回传数据
                SendData((ushort)(comboBox3.SelectedIndex + 1));
                sensorID = (byte)(comboBox3.SelectedIndex + 1);
                readBackFlag = 0;

                // 异步等待100毫秒
                await Task.Delay(100);
                // 读取串口回传数据
                ReadSerialData();

                if (readBackFlag > 0)
                {
                    MessageBox.Show("地址修改成功");
                    comboBox1.SelectedIndex = comboBox3.SelectedIndex;
                }
                else
                {
                    MessageBox.Show("地址修改失败");
                }
            }
            else
            {
                MessageBox.Show("请打开串口");
            }



        }

        private void button7_Click(object sender, EventArgs e)
        {
            autoAskFlag = 1;
            button7.Enabled = false;
            comboBox1.Enabled = false;
            comboBox2.Enabled = false;
            comboBox3.Enabled = false;

        }


        private async void processingtextbox3()
        {
            float[] parameter = new float[1];
            byte[] byteArray = new byte[13];
            // 仅在回车键触发时执行逻辑
            if (enterPressed)
            {
                enterPressed = false; // 重置标志
                if (string.IsNullOrEmpty(textBox3.Text))
                {
                    MessageBox.Show("输入参数为空");
                }
                else
                {
                    if (serialPort.IsOpen)
                    {
                        byteArray[0] = (byte)(comboBox1.SelectedIndex + 1);
                        byteArray[1] = 0x10;
                        byteArray[2] = 0x00;
                        byteArray[3] = 0x02;
                        byteArray[4] = 0x00;
                        byteArray[5] = 0x02;
                        byteArray[6] = 0x04;
                        parameter[0] = float.Parse(textBox3.Text);
                        byte[] var = BitConverter.GetBytes(parameter[0]);
                        byteArray[10] = var[0];
                        byteArray[9] = var[1];
                        byteArray[8] = var[2];
                        byteArray[7] = var[3];
                        ushort crc16val = CalculateCrc(byteArray, 11);
                        byteArray[11] = (byte)(crc16val & 0xFF);
                        byteArray[12] = (byte)(crc16val >> 8);
                        serialPort.Write(byteArray, 0, byteArray.Length);

                        // 异步等待100毫秒
                        await Task.Delay(200);
                        // 读取串口回传数据
                        SendData((ushort)(comboBox1.SelectedIndex + 1));
                        // 异步等待100毫秒
                        await Task.Delay(100);
                        // 读取串口回传数据
                        ReadSerialData();

                        if (float.Parse(textBox3.Text) == float.Parse(textBox4.Text))
                        {
                            MessageBox.Show("参数修改成功");
                        }
                        else
                        {
                            MessageBox.Show("参数修改失败");
                        }

                    }
                }
            }
        }

        private async void processingtextbox16()
        {
            float[] parameter = new float[1];
            byte[] byteArray = new byte[13];

            // 仅在回车键触发时执行逻辑
            if (enterPressed)
            {
                enterPressed = false; // 重置标志
                if (string.IsNullOrEmpty(textBox16.Text))
                {
                    MessageBox.Show("输入参数为空");
                }
                else
                {
                    if (serialPort.IsOpen)
                    {
                        byteArray[0] = (byte)(comboBox1.SelectedIndex + 1);
                        byteArray[1] = 0x10;
                        byteArray[2] = 0x00;
                        byteArray[3] = 0x03;
                        byteArray[4] = 0x00;
                        byteArray[5] = 0x02;
                        byteArray[6] = 0x04;
                        parameter[0] = float.Parse(textBox16.Text);
                        byte[] var = BitConverter.GetBytes(parameter[0]);
                        byteArray[10] = var[0];
                        byteArray[9] = var[1];
                        byteArray[8] = var[2];
                        byteArray[7] = var[3];
                        ushort crc16val = CalculateCrc(byteArray, 11);
                        byteArray[11] = (byte)(crc16val & 0xFF);
                        byteArray[12] = (byte)(crc16val >> 8);
                        serialPort.Write(byteArray, 0, byteArray.Length);

                        // 异步等待100毫秒
                        await Task.Delay(200);
                        // 读取串口回传数据
                        SendData((ushort)(comboBox1.SelectedIndex + 1));
                        // 异步等待100毫秒
                        await Task.Delay(100);
                        // 读取串口回传数据
                        ReadSerialData();

                        if (float.Parse(textBox16.Text) == float.Parse(textBox15.Text))
                        {
                            MessageBox.Show("参数修改成功");
                        }
                        else
                        {
                            MessageBox.Show("参数修改失败");
                        }

                    }
                }
            }
        }

        private async void processingtextbox18()
        {
            float[] parameter = new float[1];
            byte[] byteArray = new byte[13];

            // 仅在回车键触发时执行逻辑
            if (enterPressed)
            {
                enterPressed = false; // 重置标志
                if (string.IsNullOrEmpty(textBox18.Text))
                {
                    MessageBox.Show("输入参数为空");
                }
                else
                {
                    if (serialPort.IsOpen)
                    {
                        byteArray[0] = (byte)(comboBox1.SelectedIndex + 1);
                        byteArray[1] = 0x10;
                        byteArray[2] = 0x00;
                        byteArray[3] = 0x02;
                        byteArray[4] = 0x00;
                        byteArray[5] = 0x02;
                        byteArray[6] = 0x04;
                        parameter[0] = float.Parse(textBox18.Text);
                        byte[] var = BitConverter.GetBytes(parameter[0]);
                        byteArray[10] = var[0];
                        byteArray[9] = var[1];
                        byteArray[8] = var[2];
                        byteArray[7] = var[3];
                        ushort crc16val = CalculateCrc(byteArray, 11);
                        byteArray[11] = (byte)(crc16val & 0xFF);
                        byteArray[12] = (byte)(crc16val >> 8);
                        serialPort.Write(byteArray, 0, byteArray.Length);

                        // 异步等待100毫秒
                        await Task.Delay(200);
                        // 读取串口回传数据
                        SendData((ushort)(comboBox1.SelectedIndex + 1));
                        // 异步等待100毫秒
                        await Task.Delay(100);
                        // 读取串口回传数据
                        ReadSerialData();

                        if (float.Parse(textBox18.Text) == float.Parse(textBox4.Text))
                        {
                            MessageBox.Show("参数修改成功");
                        }
                        else
                        {
                            MessageBox.Show("参数修改失败");
                        }

                    }
                }
            }
        }
    }
}
