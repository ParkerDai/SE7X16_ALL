using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using SE190X;

namespace WindowsFormsApplication1
{
    public partial class Form1 : Form
    {
        // Boolean flag used to determine when a character other than a number is entered.
        private bool nonNumberEntered = false;

        private bool STOP_WHEN_FAIL = false;
        private bool WAIT = false;
        private bool Run_Stop;

        TcpClient telnet = new TcpClient();
        NetworkStream telentStream; //宣告網路資料流變數
        byte[] bytWrite_telnet;
        byte[] bytRead_telnet;

        TcpClient atoptestd = new TcpClient();
        NetworkStream atoptestdStream; //宣告網路資料流變數
        byte[] bytWrite_atoptestd;
        byte[] bytRead_atoptestd;

        public Thread rcvThread, ttlThread;

        // Ping
        System.Net.NetworkInformation.Ping objping = new System.Net.NetworkInformation.Ping();

        // C#取得主程式路徑(Application Path)
        string appPATH = Application.StartupPath;

        string fnameTmp;
        string MODEL_NAME, TARGET_IP, TARGET_eutIP;   // MODEL_NAME 程式測試&判斷使用，由文字檔檔名決定，強制大寫
        string model_name;              // model_name 出廠設定使用，由文字檔內文第一行決定，強制大寫
        static uint dev_num = 50;

        // new:建構ArrayList物件
        ArrayList TEST_STATUS = new ArrayList(50); // 0:未測試,1:PASS,2:fail,3:error

        ArrayList TEST_FunLog = new ArrayList(50);
        int idx_funlog;
        string[] TEST_RESULT;

        public Label[] lblFunction = new Label[dev_num];

        string COM_function, CAN_function, CAN_loopback, SD_function, USB_function, SATA_function, WaitKey, USR, PWD;
        string data;
        int TestFun_MaxIdx;
        int row_num;
        int MOUSE_Idx, Test_Idx;
        DateTime time;
        Process proc;
        int[] COM_PID = new int[2];
        int secretX;
        bool chooseStart = false;
        string tester_forExcel, productNum_forExcel, coreSN_forExcel, lanSN_forExcel, uartSN_forExcel, serial1SN_forExcel, serial2SN_forExcel, serial3SN_forExcel, serial4SN_forExcel, Mac_forExcel;
        string startTime, endTime;

        string rxContents;
        string rxContents_EUT;

        public Form1()
        {
            InitializeComponent();
            // 表單中的焦點永遠在某個控制項上
            //this.Activated += new EventHandler(delegate(object o, EventArgs e)
            //{
            //    this.txt_Tx.Focus();
            //});
            //this.txt_Tx.Leave += new EventHandler(delegate(object o, EventArgs e)
            //{
            //    this.txt_Tx.Focus();
            //});
        }

        public delegate void myUICallBack(string myStr, TextBox txt); // delegate 委派；Invoke 調用

        /// <summary>
        /// 更新主線程的UI (txt_Rx.text) = Display
        /// </summary>
        /// <param name="myStr">字串</param>
        /// <param name="txt">指定的控制項，限定有Text屬性</param>
        public void myUI(string myStr, TextBox txt)
        {
            if (txt.InvokeRequired)    // if (this.InvokeRequired)
            {
                myUICallBack myUpdate = new myUICallBack(myUI);
                this.Invoke(myUpdate, myStr, txt);
            }
            else
            {
                int i;
                string[] line;
                int ptr = myStr.IndexOf("\r\n", 0); // vb6: ptr = InStr(1, keyword, vbCrLf, vbTextCompare)
                //Debug.Print(ptr.ToString());
                if (ptr == -1)  // Instr與IndexOf的起始位置不同，結果的表達也不同(參見MSDN)
                {
                    ptr = myStr.IndexOf(((char)27).ToString() + ((char)91).ToString() + ((char)74).ToString(), 0); // ←[J
                    if (ptr != -1)
                        ptr = ptr + 2;
                }
                // 判斷 txt_Rx.Text 中的字串是否超出最大長度
                if (txt.Text.Length + myStr.Length >= txt.MaxLength)
                {
                    if (myStr.Length >= txt.MaxLength)
                        //txt.Text = myStr.Substring(myStr.Length - 1 - txt.MaxLength, txt.MaxLength); // 右邊(S.Length-1-指定長度，指定長度)
                        txt.Text = myStr.Substring((myStr.Length - txt.MaxLength));
                    else if (txt.Text.Length >= myStr.Length)
                        //txt.Text = txt.Text.Substring(txt.Text.Length - 1 - (txt.Text.Length - myStr.Length), (txt.Text.Length - myStr.Length));
                        txt.Text = txt.Text.Substring((txt.Text.Length - (txt.Text.Length - myStr.Length)));
                    else
                        //txt.Text = txt.Text.Substring(txt.Text.Length - 1 - (txt.MaxLength - myStr.Length), (txt.MaxLength - myStr.Length));
                        txt.Text = txt.Text.Substring((txt.Text.Length - (txt.MaxLength - myStr.Length)));
                }
                txt.Text = txt.Text + myStr;

                // 處理((char)8)，例如開機倒數321訊息
                // Bug!!!!! 快速接收到連續的重複訊息，會讓執行緒暴增...SE7416暫時不進入此段程式碼
                if (!MODEL_NAME.Contains("SE7416"))
                {
                    int ptr1 = myStr.IndexOf(((char)8).ToString(), 0);
                    if (ptr1 != -1)
                    {
                        while (((txt_Rx.Text.IndexOf(((char)8).ToString(), 0) + 1) > 0))
                        {
                            ptr1 = (txt_Rx.Text.IndexOf(((char)8).ToString(), 0) + 1);
                            if ((ptr1 > 1))
                            {
                                txt_Rx.Text = (txt_Rx.Text.Substring(0, (ptr1 - 2)) + txt_Rx.Text.Substring((txt_Rx.Text.Length - (txt_Rx.Text.Length - ptr1))));
                                Debug.Print(txt_Rx.Text);
                            }
                            else
                            {
                                txt_Rx.Text = (txt_Rx.Text.Substring(0, (ptr1 - 1)) + txt_Rx.Text.Substring((txt_Rx.Text.Length - (txt_Rx.Text.Length - ptr1))));
                                Debug.Print(txt_Rx.Text);
                            }
                        }
                    }
                }

                data = data + myStr;
                //Console.WriteLine(data);
                if (ptr == -1 || ptr == 0)
                {
                    return;
                }

                // 處理終端機上下鍵的動作(顯示上一個指令)
                if (myStr.IndexOf(((char)27).ToString() + ((char)91).ToString() + ((char)74).ToString()) != -1)
                {
                    line = txt_Rx.Text.Split('\r');
                    txt_Rx.Text = string.Empty;     // 文字會重複的問題
                    string Rx_tmp = string.Empty;   // Rx_tmp 解決卷軸滾動視覺效果
                    for (i = 1; i < line.GetUpperBound(0) - 1; i++)
                    {
                        Rx_tmp = Rx_tmp + "\r\n" + line[i];
                    }
                    txt_Rx.Text = Rx_tmp + "\r\n" + line[i + 1].Replace(((char)27).ToString() + ((char)91).ToString() + ((char)74).ToString(), "");
                }

                // 開機完，自動輸入USR、PWD
                if (WAIT)
                {
                    if (WaitKey == null)
                    {
                        WaitKey = string.Empty;
                    }
                    else if (WaitKey != string.Empty)
                    {
                        if (data.Contains(WaitKey))
                        {
                            if (WaitKey.Equals("login", StringComparison.OrdinalIgnoreCase))
                            {
                                if (serialPort1.IsOpen)
                                {
                                    //serialPort1.DiscardOutBuffer(); // 捨棄序列驅動程式發送的緩衝區的資料
                                    if (!String.IsNullOrEmpty(USR)) //USR!=null || USR!=string.empty
                                    {
                                        serialPort1.Write(USR + ((char)13).ToString());
                                        System.Threading.Thread.Sleep(100);
                                        serialPort1.Write(PWD + ((char)13).ToString());
                                    }
                                    else
                                    {
                                        serialPort1.Write("root" + ((char)13).ToString());
                                        System.Threading.Thread.Sleep(100);
                                        serialPort1.Write("root" + ((char)13).ToString());
                                    }
                                }
                            }
                            WaitKey = string.Empty;
                            WAIT = false;       // debug: check "data"
                        }
                    }
                }
                data = myStr.Substring((myStr.Length - (myStr.Length - ptr)));
                //Debug.Print(data);
            }
        }

        private void serialPort1_Display(object sender, EventArgs e)
        {
            int i;
            string[] line;
            int ptr = rxContents.IndexOf("\r\n", 0); // vb6: ptr = InStr(1, keyword, vbCrLf, vbTextCompare)
            //Debug.Print(ptr.ToString());
            if (ptr == -1)  // Instr與IndexOf的起始位置不同，結果的表達也不同(參見MSDN)
            {
                ptr = rxContents.IndexOf(((char)27).ToString() + ((char)91).ToString() + ((char)74).ToString(), 0); // ←[J
                if (ptr != -1)
                    ptr = ptr + 2;
            }
            // 判斷 txt_Rx.Text 中的字串是否超出最大長度
            if (txt_Rx.Text.Length + rxContents.Length >= txt_Rx.MaxLength)
            {
                if (rxContents.Length >= txt_Rx.MaxLength)
                    //txt.Text = myStr.Substring(myStr.Length - 1 - txt.MaxLength, txt.MaxLength); // 右邊(S.Length-1-指定長度，指定長度)
                    txt_Rx.Text = rxContents.Substring((rxContents.Length - txt_Rx.MaxLength));
                else if (txt_Rx.Text.Length >= rxContents.Length)
                    //txt.Text = txt.Text.Substring(txt.Text.Length - 1 - (txt.Text.Length - myStr.Length), (txt.Text.Length - myStr.Length));
                    txt_Rx.Text = txt_Rx.Text.Substring((txt_Rx.Text.Length - (txt_Rx.Text.Length - rxContents.Length)));
                else
                    //txt.Text = txt.Text.Substring(txt.Text.Length - 1 - (txt.MaxLength - myStr.Length), (txt.MaxLength - myStr.Length));
                    txt_Rx.Text = txt_Rx.Text.Substring((txt_Rx.Text.Length - (txt_Rx.MaxLength - rxContents.Length)));
            }
            txt_Rx.Text = txt_Rx.Text + rxContents;

            // 處理((char)8)，例如開機倒數321訊息
            // Bug!!!!! 快速接收到連續的重複訊息，會讓執行緒暴增...SE7416暫時不進入此段程式碼
            if (!MODEL_NAME.Contains("SE7416"))
            {
                int ptr1 = rxContents.IndexOf(((char)8).ToString(), 0);
                if (ptr1 != -1)
                {
                    while (((txt_Rx.Text.IndexOf(((char)8).ToString(), 0) + 1) > 0))
                    {
                        ptr1 = (txt_Rx.Text.IndexOf(((char)8).ToString(), 0) + 1);
                        if ((ptr1 > 1))
                        {
                            txt_Rx.Text = (txt_Rx.Text.Substring(0, (ptr1 - 2)) + txt_Rx.Text.Substring((txt_Rx.Text.Length - (txt_Rx.Text.Length - ptr1))));
                            Debug.Print(txt_Rx.Text);
                        }
                        else
                        {
                            txt_Rx.Text = (txt_Rx.Text.Substring(0, (ptr1 - 1)) + txt_Rx.Text.Substring((txt_Rx.Text.Length - (txt_Rx.Text.Length - ptr1))));
                            Debug.Print(txt_Rx.Text);
                        }
                    }
                }
            }

            data = data + rxContents;
            //Console.WriteLine(data);
            if (ptr == -1 || ptr == 0)
            {
                return;
            }

            // 處理終端機上下鍵的動作(顯示上一個指令)
            if (rxContents.IndexOf(((char)27).ToString() + ((char)91).ToString() + ((char)74).ToString()) != -1)
            {
                line = txt_Rx.Text.Split('\r');
                txt_Rx.Text = string.Empty;     // 文字會重複的問題
                string Rx_tmp = string.Empty;   // Rx_tmp 解決卷軸滾動視覺效果
                for (i = 1; i < line.GetUpperBound(0) - 1; i++)
                {
                    Rx_tmp = Rx_tmp + "\r\n" + line[i];
                }
                txt_Rx.Text = Rx_tmp + "\r\n" + line[i + 1].Replace(((char)27).ToString() + ((char)91).ToString() + ((char)74).ToString(), "");
            }

            // 開機完，自動輸入USR、PWD
            if (WAIT)
            {
                if (WaitKey == null)
                {
                    WaitKey = string.Empty;
                }
                else if (WaitKey != string.Empty)
                {
                    if (data.Contains(WaitKey))
                    {
                        if (WaitKey.Equals("login", StringComparison.OrdinalIgnoreCase))
                        {
                            if (serialPort1.IsOpen)
                            {
                                //serialPort1.DiscardOutBuffer(); // 捨棄序列驅動程式發送的緩衝區的資料
                                if (!String.IsNullOrEmpty(USR)) //USR!=null || USR!=string.empty
                                {
                                    serialPort1.Write(USR + ((char)13).ToString());
                                    System.Threading.Thread.Sleep(100);
                                    serialPort1.Write(PWD + ((char)13).ToString());
                                }
                                else
                                {
                                    serialPort1.Write("root" + ((char)13).ToString());
                                    System.Threading.Thread.Sleep(100);
                                    serialPort1.Write("root" + ((char)13).ToString());
                                }
                            }
                        }
                        WaitKey = string.Empty;
                        WAIT = false;       // debug: check "data"
                    }
                }
            }
            data = rxContents.Substring((rxContents.Length - (rxContents.Length - ptr)));
            //Debug.Print(data);
        }

        private void serialPort2_Display(object sender, EventArgs e)
        {
            int i;
            string[] line;
            int ptr = rxContents_EUT.IndexOf("\r\n", 0); // vb6: ptr = InStr(1, keyword, vbCrLf, vbTextCompare)
            //Debug.Print(ptr.ToString());
            if (ptr == -1)  // Instr與IndexOf的起始位置不同，結果的表達也不同(參見MSDN)
            {
                ptr = rxContents_EUT.IndexOf(((char)27).ToString() + ((char)91).ToString() + ((char)74).ToString(), 0); // ←[J
                if (ptr != -1)
                    ptr = ptr + 2;
            }
            // 判斷 txt_Rx_EUT.Text 中的字串是否超出最大長度
            if (txt_Rx_EUT.Text.Length + rxContents_EUT.Length >= txt_Rx_EUT.MaxLength)
            {
                if (rxContents_EUT.Length >= txt_Rx_EUT.MaxLength)
                    //txt.Text = myStr.Substring(myStr.Length - 1 - txt.MaxLength, txt.MaxLength); // 右邊(S.Length-1-指定長度，指定長度)
                    txt_Rx_EUT.Text = rxContents_EUT.Substring((rxContents_EUT.Length - txt_Rx_EUT.MaxLength));
                else if (txt_Rx_EUT.Text.Length >= rxContents_EUT.Length)
                    //txt.Text = txt.Text.Substring(txt.Text.Length - 1 - (txt.Text.Length - myStr.Length), (txt.Text.Length - myStr.Length));
                    txt_Rx_EUT.Text = txt_Rx_EUT.Text.Substring((txt_Rx_EUT.Text.Length - (txt_Rx_EUT.Text.Length - rxContents_EUT.Length)));
                else
                    //txt.Text = txt.Text.Substring(txt.Text.Length - 1 - (txt.MaxLength - myStr.Length), (txt.MaxLength - myStr.Length));
                    txt_Rx_EUT.Text = txt_Rx_EUT.Text.Substring((txt_Rx_EUT.Text.Length - (txt_Rx_EUT.MaxLength - rxContents_EUT.Length)));
            }
            txt_Rx_EUT.Text = txt_Rx_EUT.Text + rxContents_EUT;

            // 處理((char)8)，例如開機倒數321訊息
            // Bug!!!!! 快速接收到連續的重複訊息，會讓執行緒暴增...SE7416暫時不進入此段程式碼
            //if (!MODEL_NAME.Contains("SE7416"))
            //{
            int ptr1 = rxContents_EUT.IndexOf(((char)8).ToString(), 0);
            if (ptr1 != -1)
            {
                while (((txt_Rx_EUT.Text.IndexOf(((char)8).ToString(), 0) + 1) > 0))
                {
                    ptr1 = (txt_Rx_EUT.Text.IndexOf(((char)8).ToString(), 0) + 1);
                    if ((ptr1 > 1))
                    {
                        txt_Rx_EUT.Text = (txt_Rx_EUT.Text.Substring(0, (ptr1 - 2)) + txt_Rx_EUT.Text.Substring((txt_Rx_EUT.Text.Length - (txt_Rx_EUT.Text.Length - ptr1))));
                        Debug.Print(txt_Rx_EUT.Text);
                    }
                    else
                    {
                        txt_Rx_EUT.Text = (txt_Rx_EUT.Text.Substring(0, (ptr1 - 1)) + txt_Rx_EUT.Text.Substring((txt_Rx_EUT.Text.Length - (txt_Rx_EUT.Text.Length - ptr1))));
                        Debug.Print(txt_Rx_EUT.Text);
                    }
                }
            }
            //}

            data = data + rxContents_EUT;
            //Console.WriteLine(data);
            if (ptr == -1 || ptr == 0)
            {
                return;
            }

            // 處理終端機上下鍵的動作(顯示上一個指令)
            if (rxContents_EUT.IndexOf(((char)27).ToString() + ((char)91).ToString() + ((char)74).ToString()) != -1)
            {
                line = txt_Rx_EUT.Text.Split('\r');
                txt_Rx_EUT.Text = string.Empty;     // 文字會重複的問題
                string Rx_tmp = string.Empty;   // Rx_tmp 解決卷軸滾動視覺效果
                for (i = 1; i < line.GetUpperBound(0) - 1; i++)
                {
                    Rx_tmp = Rx_tmp + "\r\n" + line[i];
                }
                txt_Rx_EUT.Text = Rx_tmp + "\r\n" + line[i + 1].Replace(((char)27).ToString() + ((char)91).ToString() + ((char)74).ToString(), "");
            }

            // 開機完，自動輸入USR、PWD
            if (WAIT)
            {
                if (WaitKey == null)
                {
                    WaitKey = string.Empty;
                }
                else if (WaitKey != string.Empty)
                {
                    if (data.Contains(WaitKey))
                    {
                        if (WaitKey.Equals("login", StringComparison.OrdinalIgnoreCase))
                        {
                            if (serialPort1.IsOpen)
                            {
                                //serialPort1.DiscardOutBuffer(); // 捨棄序列驅動程式發送的緩衝區的資料
                                if (!String.IsNullOrEmpty(USR)) //USR!=null || USR!=string.empty
                                {
                                    serialPort1.Write(USR + ((char)13).ToString());
                                    System.Threading.Thread.Sleep(100);
                                    serialPort1.Write(PWD + ((char)13).ToString());
                                }
                                else
                                {
                                    serialPort1.Write("root" + ((char)13).ToString());
                                    System.Threading.Thread.Sleep(100);
                                    serialPort1.Write("root" + ((char)13).ToString());
                                }
                            }
                        }
                        WaitKey = string.Empty;
                        WAIT = false;       // debug: check "data"
                    }
                }
            }
            data = rxContents_EUT.Substring((rxContents_EUT.Length - (rxContents_EUT.Length - ptr)));
            //Debug.Print(data);
        }

        public void RecNote(int idx, string note)
        {
            string tmpNote = string.Empty;
            DateTime time = DateTime.Now;
            tmpNote = String.Format("{0:00}:{1:00}:{2:00}", time.Hour, time.Minute, time.Second) + " [" + lblFunction[idx].Tag + "]" + ": " + note + "\r\n";
            noteUI(tmpNote, txt_Note);
        }

        public delegate void noteUICallBack(string myStr, TextBox txt); // delegate 委派；Invoke 調用

        /// <summary>
        /// 更新主線程的UI (txt_Note.text)
        /// </summary>
        /// <param name="myStr">字串</param>
        /// <param name="txt">指定的控制項，限定有Text屬性</param>
        public void noteUI(string myStr, TextBox txt)
        {
            if (txt.InvokeRequired)    // if (this.InvokeRequired)
            {
                noteUICallBack myUpdate = new noteUICallBack(noteUI);
                this.Invoke(myUpdate, myStr, txt);
            }
            else
            {
                // 判斷 txt.Text 中的字串是否超出最大長度
                if (txt.Text.Length + myStr.Length >= txt.MaxLength)
                {
                    if (myStr.Length >= txt.MaxLength)
                        //txt.Text = myStr.Substring(myStr.Length - 1 - txt.MaxLength, txt.MaxLength); // 右邊(S.Length-1-指定長度，指定長度)
                        txt.Text = myStr.Substring((myStr.Length - txt.MaxLength));
                    else if (txt.Text.Length >= myStr.Length)
                        //txt.Text = txt.Text.Substring(txt.Text.Length - 1 - (txt.Text.Length - myStr.Length), (txt.Text.Length - myStr.Length));
                        txt.Text = txt.Text.Substring((txt.Text.Length - (txt.Text.Length - myStr.Length)));
                    else
                        //txt.Text = txt.Text.Substring(txt.Text.Length - 1 - (txt.MaxLength - myStr.Length), (txt.MaxLength - myStr.Length));
                        txt.Text = txt.Text.Substring((txt.Text.Length - (txt.MaxLength - myStr.Length)));
                }
                txt.Text = txt.Text + myStr;
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.Cursor = Cursors.WaitCursor;   // 漏斗指標
            consoleToolStripMenuItem_CheckStateChanged(null, null);

            //COM_function = string.Empty;
            //CAN_function = string.Empty;
            //CAN_loopback = string.Empty;
            //SD_function = string.Empty;
            //USB_function = string.Empty;
            //SATA_function = string.Empty;

            // 獲取電腦的有效串口
            string[] ports = SerialPort.GetPortNames();
            foreach (string port in ports)
            {
                cmbDutCom.Items.Add(port);
                cmbEutCom.Items.Add(port);
            }
            cmbDutCom.Sorted = true;
            cmbDutCom.SelectedIndex = 0;
            cmbEutCom.Sorted = true;
            cmbEutCom.SelectedIndex = 1;

            if (IsIP(txtDutIP.Text))
            {
                TARGET_IP = txtDutIP.Text;
            }
            if (IsIP(txtEutIP.Text))
            {
                TARGET_eutIP = txtEutIP.Text;
            }

            this.Cursor = Cursors.Default;      // 還原預設指標
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            Application.Exit();
        }

        #region Shell

        private int Shell(string FilePath, string FileName)
        {
            try
            {
                ////////////////////// like VB 【shell】 ///////////////////////
                //System.Diagnostics.Process proc = new System.Diagnostics.Process();
                proc = new Process();
                proc.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
                proc.EnableRaisingEvents = false;
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.FileName = FilePath + "\\" + FileName;
                proc.Start();
                return proc.Id;
                ////////////////////////////////////////////////////////////////
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + " ' " + FileName + " ' ", "Shell error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 0;
            }
        }

        private void CloseShell(int pid)
        {
            //if (!Process.GetProcessById(pid).HasExited)
            //{
            //    // Close process by sending a close message to its main window.
            //    Process.GetProcessById(pid).CloseMainWindow();
            //    Process.GetProcessById(pid).WaitForExit(3000);
            //}
            if (!Process.GetProcessById(pid).HasExited)
            {
                Process.GetProcessById(pid).Kill();
                Process.GetProcessById(pid).WaitForExit(1000);
            }
        }

        #endregion Shell

        private void cmdOpeFile_Click(object sender, EventArgs e)
        {
            string[] cmd;
            int n = 0;
            String line;
            STOP_WHEN_FAIL = false;

            openFileDialog1.FileName = string.Empty;
            openFileDialog1.Multiselect = false;
            openFileDialog1.InitialDirectory = appPATH;
            openFileDialog1.Filter = "純文字檔(*.txt)|*.txt|All(*.*)|*.*";
            try
            {
                if (openFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    fnameTmp = openFileDialog1.SafeFileName;
                    //fnameTmp = openFileDialog1.FileName.Replace(appPATH + "\\", string.Empty);
                    cmdOpeFile.Text = fnameTmp;

                    // Pass the file path and file name to the StreamReader constructor
                    using (StreamReader sr = new StreamReader(fnameTmp, Encoding.ASCII))
                    {
                        // 1. Read the first line of text
                        line = sr.ReadLine();
                        cmd = line.Split(' ');
                        if (cmd.GetUpperBound(0) < 1)
                        {
                            MessageBox.Show("檔案第一行錯誤，格式應該為 Model IP User Password ", "Error Message");
                            sr.Close();
                            return;
                        }
                        else
                            if (!IsIP(cmd[1]))
                            {
                                MessageBox.Show("檔案第一行錯誤，請檢查 IP 是否輸入正確 ", "Error Message");
                                sr.Close();
                                return;
                            }

                        Shell(appPATH, "arp-d.bat");
                        // model_name 出廠設定使用，由文字檔內文第一行決定，強制大寫
                        model_name = cmd[0].ToUpper();
                        // MODEL_NAME 程式測試&判斷使用，由文字檔檔名決定，強制大寫
                        MODEL_NAME = (fnameTmp.Replace(".txt", string.Empty)).ToUpper();
                        TARGET_IP = cmd[1];
                        USR = cmd[2];
                        if (cmd.GetUpperBound(0) > 2) { PWD = cmd[3]; }
                        else { PWD = string.Empty; }

                        this.Text = MODEL_NAME + " ";
                        chkLoop.Checked = false;
                        Test_Idx = 0;
                        Run_Stop = true;    //?
                        //SYSTEM = 0;
                        serialPort1_Close();
                        //if (serialPort1.IsOpen) { serialPort1.Close(); }
                        serialPort2_Close();
                        //if (serialPort2.IsOpen) { serialPort2.Close(); }
                        if (telnet.Connected) { telnet.Close(); }

                        MappingFunction();

                        RemoveControl(TestFun_MaxIdx);   // Initial Label
                        txt_Note.Text = string.Empty;
                        txt_Rx.Text = string.Empty;
                        TEST_STATUS.Clear();    // 將所有元素移除(Initial)
                        TEST_FunLog.Clear();

                        // 2. Continue to read until you reach end of file
                        line = sr.ReadLine();
                        while (line != null)
                        {
                            if (line != string.Empty)
                            {
                                cmd = line.Split(' ');
                                switch (cmd[0].ToUpper())
                                {
                                    case "BUZZER":
                                        AddFunction(line, cmd[0], n);
                                        n = n + 1;
                                        TEST_STATUS.Add(0);
                                        TEST_FunLog.Add("BUZZER");
                                        break;
                                    case "CONSOLE-DUT":
                                        AddFunction(line, cmd[0], n);
                                        n = n + 1;
                                        TEST_STATUS.Add(0); // 0:將TEST_STATUS狀態設定為未測試
                                        break;
                                    case "CONSOLE-EUT":
                                        AddFunction(line, cmd[0], n);
                                        n = n + 1;
                                        TEST_STATUS.Add(0); // 0:將TEST_STATUS狀態設定為未測試
                                        break;
                                    case "CPU":
                                        AddFunction(line, cmd[0], n);
                                        n = n + 1;
                                        TEST_STATUS.Add(0);
                                        break;
                                    case "COM":
                                        AddFunction(line, cmd[0], n);
                                        n = n + 1;
                                        TEST_STATUS.Add(0);
                                        TEST_FunLog.Add("COM");
                                        break;
                                    case "COMTOCOM":
                                        AddFunction(line, cmd[0], n);
                                        n = n + 1;
                                        TEST_STATUS.Add(0);
                                        TEST_FunLog.Add("COMTOCOM" + "(" + cmd[2].ToUpper() + ")");
                                        break;
                                    case "CANTOCAN":
                                        AddFunction(line, cmd[0], n);
                                        n = n + 1;
                                        TEST_STATUS.Add(0);
                                        TEST_FunLog.Add("CANTOCAN");
                                        break;
                                    case "DI":
                                        AddFunction(line, cmd[0], n);
                                        n = n + 1;
                                        TEST_STATUS.Add(0);
                                        TEST_FunLog.Add("DI");
                                        break;
                                    case "DO":  // =Relay
                                        AddFunction(line, cmd[0], n);
                                        n = n + 1;
                                        TEST_STATUS.Add(0);
                                        TEST_FunLog.Add("DO");
                                        break;
                                    case "DOTODI":  // =DIO
                                        AddFunction(line, cmd[0], n);
                                        n = n + 1;
                                        TEST_STATUS.Add(0);
                                        TEST_FunLog.Add("DOTODI");
                                        break;
                                    case "DELETE":  // delete files in jffs2
                                        AddFunction(line, cmd[0], n);
                                        n = n + 1;
                                        TEST_STATUS.Add(0);
                                        break;
                                    case "EEPROM":
                                        AddFunction(line, cmd[0], n);
                                        n = n + 1;
                                        TEST_STATUS.Add(0);
                                        TEST_FunLog.Add("EEPROM");
                                        break;
                                    case "EEPROMIC":    // check eeprom ic for SE7816
                                        AddFunction(line, cmd[0], n);
                                        n = n + 1;
                                        TEST_STATUS.Add(0);
                                        TEST_FunLog.Add("EEPROMIC");
                                        break;
                                    case "FTP":
                                        AddFunction(line, cmd[0], n);
                                        n = n + 1;
                                        TEST_STATUS.Add(0);
                                        break;
                                    case "FLASH":
                                        AddFunction(line, cmd[0], n);
                                        n = n + 1;
                                        TEST_STATUS.Add(0);
                                        break;
                                    case "FACTORYFILES":
                                        AddFunction(line, cmd[0], n);
                                        n = n + 1;
                                        TEST_STATUS.Add(0);
                                        break;
                                    case "GWD":
                                        AddFunction(line, cmd[0], n);
                                        n = n + 1;
                                        TEST_STATUS.Add(0);
                                        break;
                                    case "GETRTC":
                                        AddFunction(line, cmd[0], n);
                                        n = n + 1;
                                        TEST_STATUS.Add(0);
                                        TEST_FunLog.Add("GETRTC");
                                        break;
                                    case "GPS":
                                        AddFunction(line, cmd[0], n);
                                        n = n + 1;
                                        TEST_STATUS.Add(0);
                                        TEST_FunLog.Add("GPS");
                                        break;
                                    case "KEYPAD":
                                        AddFunction(line, cmd[0], n);
                                        n = n + 1;
                                        TEST_STATUS.Add(0);
                                        TEST_FunLog.Add("KEYPAD");
                                        break;
                                    case "LED":
                                        AddFunction(line, cmd[0], n);
                                        n = n + 1;
                                        TEST_STATUS.Add(0);
                                        TEST_FunLog.Add("LED");
                                        break;
                                    case "LCM":
                                        AddFunction(line, cmd[0], n);
                                        n = n + 1;
                                        TEST_STATUS.Add(0);
                                        TEST_FunLog.Add("LCM");
                                        break;
                                    case "LOADTOOLS":
                                        AddFunction(line, cmd[0], n);
                                        n = n + 1;
                                        TEST_STATUS.Add(0);
                                        break;
                                    case "LAN":
                                        AddFunction(line, cmd[0], n);
                                        n = n + 1;
                                        TEST_STATUS.Add(0);
                                        TEST_FunLog.Add("LAN");
                                        break;
                                    case "MEMORY":
                                        AddFunction(line, cmd[0], n);
                                        n = n + 1;
                                        TEST_STATUS.Add(0);
                                        break;
                                    case "NETWORK":
                                        AddFunction(line, cmd[0], n);
                                        n = n + 1;
                                        TEST_STATUS.Add(0);
                                        break;
                                    case "POWER":
                                        AddFunction(line, cmd[0], n);
                                        n = n + 1;
                                        TEST_STATUS.Add(0);
                                        break;
                                    case "RS485":
                                        AddFunction(line, cmd[0], n);
                                        n = n + 1;
                                        TEST_STATUS.Add(0);
                                        break;
                                    case "RS232":
                                        AddFunction(line, cmd[0], n);
                                        n = n + 1;
                                        TEST_STATUS.Add(0);
                                        break;
                                    case "RESTART":
                                        AddFunction(line, cmd[0], n);
                                        n = n + 1;
                                        TEST_STATUS.Add(0);
                                        break;
                                    case "RTC":
                                        AddFunction(line, cmd[0], n);
                                        n = n + 1;
                                        TEST_STATUS.Add(0);
                                        TEST_FunLog.Add("RTC");
                                        break;
                                    case "RESTORE":
                                        AddFunction(line, cmd[0], n);
                                        n = n + 1;
                                        TEST_STATUS.Add(0);
                                        TEST_FunLog.Add("RESTORE");
                                        break;
                                    case "SLEEP":
                                        AddFunction(line, cmd[0], n);
                                        n = n + 1;
                                        TEST_STATUS.Add(0);
                                        break;
                                    case "SYSTEM":
                                        AddFunction(line, cmd[0], n);
                                        n = n + 1;
                                        TEST_STATUS.Add(0);
                                        break;
                                    case "SD":
                                        AddFunction(line, cmd[0], n);
                                        n = n + 1;
                                        TEST_STATUS.Add(0);
                                        TEST_FunLog.Add("SD");
                                        break;
                                    case "SATA":
                                        AddFunction(line, cmd[0], n);
                                        n = n + 1;
                                        TEST_STATUS.Add(0);
                                        TEST_FunLog.Add("SATA");
                                        break;
                                    case "STOP":
                                        if (cmd[1].ToUpper() == "WHEN" && cmd[2].ToUpper() == "FAIL")
                                            STOP_WHEN_FAIL = true;
                                        break;
                                    case "TELNET":
                                        AddFunction(line, cmd[0], n);
                                        n = n + 1;
                                        TEST_STATUS.Add(0);
                                        break;
                                    case "TESTD":
                                        AddFunction(line, cmd[0], n);
                                        n = n + 1;
                                        TEST_STATUS.Add(0);
                                        break;
                                    case "USB":
                                        AddFunction(line, cmd[0], n);
                                        n = n + 1;
                                        TEST_STATUS.Add(0);
                                        TEST_FunLog.Add("USB");
                                        break;
                                    case "WATCHDOG":
                                        AddFunction(line, cmd[0], n);
                                        n = n + 1;
                                        TEST_STATUS.Add(0);
                                        TEST_FunLog.Add("WATCHDOG");
                                        break;
                                    default:
                                        break;
                                }
                            }
                            // 3. Read the next line
                            line = sr.ReadLine();
                        }
                        // 4. close the file
                        sr.Close();
                    }
                    if (n == 0)
                        return;
                    else
                        TestFun_MaxIdx = n;
                    composingTmr.Enabled = true;
                    TEST_STATUS.TrimToSize();   // TrimToSize():將容量設為實際元素數目
                    TEST_FunLog.TrimToSize();
                    TEST_RESULT = new string[TEST_FunLog.Count];

                    cmdOpeFile.Text = fnameTmp.Replace(".txt", string.Empty);

                    macEnabledStatus(MODEL_NAME);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\n" + ex.StackTrace, "");
            }
            finally
            {
                //Debug.Print("STOP_WHEN_FAIL = " + STOP_WHEN_FAIL.ToString());
                //Debug.Print("測試陣列的大小 : " + lblFun_MaxIdx.ToString());
            }
        }

        private void macEnabledStatus(string modelname)
        {
            lbl_access3.Enabled = false;
            lbl_access4.Enabled = false;
            lbl_access5.Enabled = false;
            lbl_access6.Enabled = false;
            lbl_access7.Enabled = false;
            lbl_access8.Enabled = false;
            lbl_access9.Enabled = false;
            lbl_access10.Enabled = false;
            lbl_access11.Enabled = false;
            lbl_access12.Enabled = false;
            lbl_access13.Enabled = false;
            lbl_access14.Enabled = false;
            lbl_mac3.Enabled = false;
            lbl_mac4.Enabled = false;
            lbl_mac5.Enabled = false;
            lbl_mac6.Enabled = false;
            lbl_mac7.Enabled = false;
            lbl_mac8.Enabled = false;
            lbl_mac9.Enabled = false;
            lbl_mac10.Enabled = false;
            lbl_mac11.Enabled = false;
            lbl_mac12.Enabled = false;
            lbl_mac13.Enabled = false;
            lbl_mac14.Enabled = false;
            txt_mac3.Enabled = false;
            txt_mac4.Enabled = false;
            txt_mac5.Enabled = false;
            txt_mac6.Enabled = false;
            txt_mac7.Enabled = false;
            txt_mac8.Enabled = false;
            txt_mac9.Enabled = false;
            txt_mac10.Enabled = false;
            txt_mac11.Enabled = false;
            txt_mac12.Enabled = false;
            txt_mac13.Enabled = false;
            txt_mac14.Enabled = false;
            if (modelname.Contains("SE7816"))
            {
                lbl_access3.Enabled = true;
                lbl_access4.Enabled = true;
                lbl_access5.Enabled = true;
                lbl_access6.Enabled = true;
                lbl_mac3.Enabled = true;
                lbl_mac4.Enabled = true;
                lbl_mac5.Enabled = true;
                lbl_mac6.Enabled = true;
                txt_mac3.Enabled = true;
                txt_mac4.Enabled = true;
                txt_mac5.Enabled = true;
                txt_mac6.Enabled = true;
            }
            else if (modelname.Contains("SE7416"))
            {
                lbl_access3.Enabled = true;
                lbl_access4.Enabled = true;
                lbl_mac3.Enabled = true;
                lbl_mac4.Enabled = true;
                txt_mac3.Enabled = true;
                txt_mac4.Enabled = true;
            }
            else if (modelname.Contains("SE7116"))
            {
                lbl_access3.Enabled = true;
                lbl_access4.Enabled = true;
                lbl_access5.Enabled = true;
                lbl_access6.Enabled = true;
                lbl_access7.Enabled = true;
                lbl_access8.Enabled = true;
                lbl_mac3.Enabled = true;
                lbl_mac4.Enabled = true;
                lbl_mac5.Enabled = true;
                lbl_mac6.Enabled = true;
                lbl_mac7.Enabled = true;
                lbl_mac8.Enabled = true;
                txt_mac3.Enabled = true;
                txt_mac4.Enabled = true;
                txt_mac5.Enabled = true;
                txt_mac6.Enabled = true;
                txt_mac7.Enabled = true;
                txt_mac8.Enabled = true;
            }
            else if (modelname.Contains("SE4116"))
            {
                lbl_access3.Enabled = true;
                lbl_access4.Enabled = true;
                lbl_access5.Enabled = true;
                lbl_access6.Enabled = true;
                lbl_access7.Enabled = true;
                lbl_access8.Enabled = true;
                lbl_access9.Enabled = true;
                lbl_access10.Enabled = true;
                lbl_access11.Enabled = true;
                lbl_access12.Enabled = true;
                lbl_access13.Enabled = true;
                lbl_access14.Enabled = true;
                lbl_mac3.Enabled = true;
                lbl_mac4.Enabled = true;
                lbl_mac5.Enabled = true;
                lbl_mac6.Enabled = true;
                lbl_mac7.Enabled = true;
                lbl_mac8.Enabled = true;
                lbl_mac9.Enabled = true;
                lbl_mac10.Enabled = true;
                lbl_mac11.Enabled = true;
                lbl_mac12.Enabled = true;
                lbl_mac13.Enabled = true;
                lbl_mac14.Enabled = true;
                txt_mac3.Enabled = true;
                txt_mac4.Enabled = true;
                txt_mac5.Enabled = true;
                txt_mac6.Enabled = true;
                txt_mac7.Enabled = true;
                txt_mac8.Enabled = true;
                txt_mac9.Enabled = true;
                txt_mac10.Enabled = true;
                txt_mac11.Enabled = true;
                txt_mac12.Enabled = true;
                txt_mac13.Enabled = true;
                txt_mac14.Enabled = true;
            }
        }

        /// <summary>
        /// 新增控制項 lblFunction
        /// </summary>
        /// <param name="dat">檔案名稱的設定內容，給Tag屬性</param>
        /// <param name="item_name">測試項名稱，給Text屬性</param>
        /// <param name="n">控制項陣列的索引標籤，給TabIndex屬性</param>
        public void AddFunction(string dat, string item_name, int n)
        {
            lblFunction[n] = new Label();
            lblFunction[n].AutoSize = true;
            lblFunction[n].TextAlign = ContentAlignment.MiddleCenter;
            lblFunction[n].Font = new Font("Arial", 12, FontStyle.Bold); // new Font(字型, 大小, 樣式);
            lblFunction[n].BorderStyle = BorderStyle.FixedSingle;
            lblFunction[n].Enabled = true;
            lblFunction[n].Location = new Point(12, 48);
            lblFunction[n].Visible = false;
            lblFunction[n].Tag = dat;
            lblFunction[n].BackColor = Color.FromArgb(255, 255, 255);
            lblFunction[n].Text = item_name.Substring(0, 1).ToUpper() + item_name.Substring(1, item_name.Length - 1);
            // TabIndex => ((Label)sender).TabIndex
            lblFunction[n].TabIndex = n;
            //splitContainer1.Panel1.Controls.Add(lblFunction[n]);
            tabPage5.Controls.Add(lblFunction[n]);
            // 註冊事件
            lblFunction[n].MouseMove += new MouseEventHandler(lblFunction_MouseMove);
            lblFunction[n].MouseLeave += new EventHandler(lblFunction_MouseLeave);
            lblFunction[n].MouseDown += new MouseEventHandler(lblFunction_MouseDown);

            // 連結 contextMenuStrip (右鍵選單)
            lblFunction[n].ContextMenuStrip = contextMenuStrip1;
        }

        /// <summary>
        /// 移除控制項 lblFunction
        /// </summary>
        /// <param name="MaxIdx">控制項陣列的上限值</param>
        public void RemoveControl(int MaxIdx)
        {
            int idx;
            // NOTE: The code below uses the instance of the Label from the previous example.
            for (idx = 0; idx <= MaxIdx; idx++)
            {
                //if (splitContainer1.Panel1.Controls.Contains(lblFunction[idx]))
                if (tabPage5.Controls.Contains(lblFunction[idx]))
                {
                    // 移除事件
                    this.lblFunction[idx].MouseMove -= new MouseEventHandler(lblFunction_MouseMove);
                    lblFunction[idx].MouseLeave -= new EventHandler(lblFunction_MouseLeave);
                    lblFunction[idx].MouseDown -= new MouseEventHandler(lblFunction_MouseDown);
                    splitContainer1.Panel1.Controls.Remove(lblFunction[idx]);
                    lblFunction[idx].Dispose();
                }
            }
        }

        private void lblFunction_MouseMove(object sender, MouseEventArgs e)
        {
            string dat = System.Convert.ToString(((Label)sender).Tag);
            lbl_cmdTag.Text = dat;
        }

        private void lblFunction_MouseLeave(object sender, EventArgs e)
        {
            lbl_cmdTag.Text = string.Empty;
        }

        // 單擊測試 & 右鍵選單
        private void lblFunction_MouseDown(object sender, MouseEventArgs e)
        {
            string dat = System.Convert.ToString(((Label)sender).Text);
            int idx = ((Label)sender).TabIndex;
            if (cmdStart.Enabled == false) { return; }
            if (e.Button == MouseButtons.Left)
            {
                cmdOpeFile.Enabled = false;
                cmdStart.Enabled = false;
                cmdStop.Enabled = true;
                cmdNext.Enabled = false;
                //txtDutIP.Enabled = false;
                //txtEutIP.Enabled = false;
                cmbDutCom.Enabled = false;
                cmbEutCom.Enabled = false;
                TEST_STATUS[idx] = RunTest(idx);
                cmdOpeFile.Enabled = true;
                cmdStart.Enabled = true;
                cmdStop.Enabled = true;
                cmdNext.Enabled = true;
                //txtDutIP.Enabled = true;
                //txtEutIP.Enabled = true;
                cmbDutCom.Enabled = true;
                cmbEutCom.Enabled = true;

                Run_Stop = false;
            }
            else if (e.Button == MouseButtons.Right)
            {
                MOUSE_Idx = idx;
                if (dat == "Console-DUT" || dat == "Console-EUT" || dat == "Telnet" || dat == "Power")
                {
                    用Putty開啟ToolStripMenuItem.Visible = true;
                }
                else
                {
                    用Putty開啟ToolStripMenuItem.Visible = false;
                }
            }
        }

        public void MappingFunction()
        {
            switch ((MODEL_NAME.Substring(0, 4)).ToUpper())
            {
                case "SE78":
                    COM_function = "atop_tcp_server";
                    CAN_function = "atop_cantcpsvr";
                    CAN_loopback = "atop_canloopback";
                    SD_function = "mpc8377_sd_td";
                    USB_function = "mpc8377_usb_td";
                    SATA_function = "mpc8377_sata_td";
                    break;
                case "SE74":
                    COM_function = "tcp_server";    // idt_tcp_server rs485 115200 &
                    USB_function = "usb_td";
                    break;
                case "SE71":
                    COM_function = "atop_tcp_server";
                    CAN_function = "atop_cantcpsvr";
                    CAN_loopback = "atop_canloopback";
                    SD_function = "mpc8377_sd_td";
                    USB_function = "mpc8377_usb_td";
                    SATA_function = "mpc8377_sata_td";
                    break;
                default:
                    COM_function = "atop_tcp_server";
                    CAN_function = "atop_cantcpsvr";
                    CAN_loopback = "atop_canloopback";
                    break;
            }

            txtDutIP.Text = TARGET_IP;
            string[] ip_split = new string[3];
            ip_split = TARGET_IP.Split('.');
            ip_split[3] = (Convert.ToInt32(ip_split[3]) + 2).ToString();
            txtEutIP.Text = ip_split[0] + "." + ip_split[1] + "." + ip_split[2] + "." + ip_split[3];
        }

        private void serialPort1_DataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            //string rxContents;
            if (serialPort1.BytesToRead > 0)
            {
                //int bytes = serialPort1.BytesToRead;
                //byte[] comBuffer = new byte[bytes];
                byte[] comBuffer = new byte[serialPort1.BytesToRead];
                serialPort1.Read(comBuffer, 0, comBuffer.Length);
                rxContents = Encoding.ASCII.GetString(comBuffer);

                //myUI(rxContents, txt_Rx);
                this.Invoke(new EventHandler(serialPort1_Display));
            }
        }

        private void serialPort1_Close()
        {
            if (serialPort1.IsOpen)
            {
                //serialPort1.DataReceived -= new SerialDataReceivedEventHandler(serialPort1_DataReceived);
                Hold(100);
                serialPort1.Close();
                終端機指令.Enabled = false;
            }
        }

        private void serialPort2_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (serialPort2.BytesToRead > 0)
            {
                byte[] comBuffer = new byte[serialPort2.BytesToRead];
                serialPort2.Read(comBuffer, 0, comBuffer.Length);
                rxContents_EUT = Encoding.ASCII.GetString(comBuffer);

                //myUI(rxContents_EUT, txt_Rx_EUT);
                this.Invoke(new EventHandler(serialPort2_Display));
            }
        }

        private void serialPort2_Close()
        {
            if (serialPort2.IsOpen)
            {
                serialPort2.DataReceived -= new SerialDataReceivedEventHandler(serialPort2_DataReceived);
                Hold(100);
                serialPort2.Close();
            }
        }

        private void telnet_Receive()
        {
            string rdData = string.Empty;
            while (true)
            {
                try
                {
                    Array.Resize(ref bytRead_telnet, telnet.ReceiveBufferSize); // Array.Resize等於vb的ReDim
                    telentStream.Read(bytRead_telnet, 0, telnet.ReceiveBufferSize);
                    rdData = (System.Text.Encoding.Default.GetString(bytRead_telnet));
                    myUI(rdData, txt_Rx);
                    Array.Clear(bytRead_telnet, 0, telnet.ReceiveBufferSize);
                    Thread.Sleep(100);
                }
                catch (Exception)
                {
                    //throw;
                }
            }
        }

        private void atoptestd_Receive()
        {
            string rdData = string.Empty;
            while (true)
            {
                try
                {
                    Array.Resize(ref bytRead_atoptestd, atoptestd.ReceiveBufferSize); // Array.Resize等於vb的ReDim
                    atoptestdStream.Read(bytRead_atoptestd, 0, atoptestd.ReceiveBufferSize);
                    rdData = (System.Text.Encoding.ASCII.GetString(bytRead_atoptestd));
                    myUI(rdData, txt_Rx);
                    Array.Clear(bytRead_atoptestd, 0, atoptestd.ReceiveBufferSize);
                    Thread.Sleep(100);
                }
                catch (Exception)
                {
                    //throw;
                }
            }
        }

        /// <summary>
        /// 發送指令
        /// </summary>
        /// <param name="cmd">Command</param>
        public void SendCmd(string cmd)
        {
            if (serialPort1.IsOpen)
            {
                if (cmd.StartsWith(((char)27).ToString()))
                {
                    serialPort1.Write(cmd);
                }
                else
                {
                    try
                    {
                        serialPort1.Write(cmd);
                        serialPort1.Write(((char)13).ToString());
                    }
                    catch (IOException)
                    {
                        // 因為執行緒結束或應用程式要求，所以已中止 I/O 操作
                        serialPort1_Close();
                        serialPort1.Open();
                        //serialPort1.DataReceived += new SerialDataReceivedEventHandler(serialPort1_DataReceived);
                    }
                }
            }
            else if (telnet != null && telnet.Connected)
            {
                if (cmd.StartsWith(((char)27).ToString()))
                {
                    bytWrite_telnet = System.Text.Encoding.Default.GetBytes(cmd);
                    telentStream.Write(bytWrite_telnet, 0, bytWrite_telnet.Length);
                }
                else
                {
                    bytWrite_telnet = System.Text.Encoding.Default.GetBytes(cmd + ((char)13).ToString());
                    telentStream.Write(bytWrite_telnet, 0, bytWrite_telnet.Length);
                }
            }
        }

        public int SendTestd(string cmd)
        {
            try
            {
                bytWrite_atoptestd = System.Text.Encoding.ASCII.GetBytes(cmd);
                atoptestdStream.Write(bytWrite_atoptestd, 0, bytWrite_atoptestd.Length);
            }
            catch (Exception ex)
            {
                MessageBox.Show("請先點選 Testd 項目進行連線", "", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 3;
            }

            return 1;
        }

        private void cmdStart_Click(object sender, EventArgs e)
        {
            int idx;
            cmdOpeFile.Enabled = false;
            cmdStart.Enabled = false;
            cmdStop.Enabled = true;
            cmdNext.Enabled = false;
            //txtDutIP.Enabled = false;
            //txtEutIP.Enabled = false;
            cmbDutCom.Enabled = false;
            cmbEutCom.Enabled = false;
            Run_Stop = false;
            try
            {
                tabControl2.SelectedTab = tabPage5;
                Hold(10);
                time = DateTime.Now;
                startTime = String.Format("{0:00}/{1:00}" + ((char)10).ToString() + "{2:00}:{3:00}:{4:00}", time.Month, time.Day, time.Hour, time.Minute, time.Second);
                if (!chooseStart)
                {
                    for (idx = 0; idx < TestFun_MaxIdx; idx++)
                    {
                        if (!lblFunction[idx].Text.ToUpper().Contains("CONSOLE") || !lblFunction[idx].Text.ToUpper().Contains("TELNET"))
                        {
                            lblFunction[idx].BackColor = Color.FromArgb(255, 255, 255);
                        }
                    }
                    Hold(1);
                }
                retest:
                for (idx = Test_Idx; idx < TestFun_MaxIdx; idx++)
                {
                    TEST_STATUS[idx] = RunTest(idx);
                    if (Run_Stop)
                    {
                        return;
                    }
                    if (STOP_WHEN_FAIL && Convert.ToInt32(TEST_STATUS[idx]) == 2)
                    {
                        break;
                    }
                    Hold(1000);
                }
                if (chkLoop.CheckState == CheckState.Checked && Run_Stop == false)
                {
                    for (idx = 0; idx < TestFun_MaxIdx; idx++)
                    {
                        if (!lblFunction[idx].Text.ToUpper().Contains("CONSOLE") && !lblFunction[idx].Text.ToUpper().Contains("TELNET"))
                        {
                            lblFunction[idx].BackColor = Color.FromArgb(255, 255, 255);
                        }
                    }
                    Test_Idx = 0;
                    goto retest;
                }
            }
            finally
            {
                cmdOpeFile.Enabled = true;
                cmdStart.Enabled = true;
                cmdStop.Enabled = true;
                cmdNext.Enabled = true;
                //txtDutIP.Enabled = true;
                //txtEutIP.Enabled = true;
                cmbDutCom.Enabled = true;
                cmbEutCom.Enabled = true;
                if (telnet.Connected) { telnet.Close(); }
                Test_Idx = 0;
                chooseStart = false;
            }
        }

        private void cmdStop_Click(object sender, EventArgs e)
        {
            try
            {
                Run_Stop = true;
                WAIT = false;
                SendCmd(((char)3).ToString()); // ((char)3):Ctrl+c
                Shell(appPATH, "arp-d.bat");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Stop error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                cmdOpeFile.Enabled = true;
                cmdStart.Enabled = true;
                cmdStop.Enabled = true;
                cmdNext.Enabled = true;
                //txtDutIP.Enabled = true;
                //txtEutIP.Enabled = true;
                cmbDutCom.Enabled = true;
                cmbEutCom.Enabled = true;
            }
        }

        /// <summary>
        /// 功能測試
        /// </summary>
        /// <param name="idx">控制項(lblFunction)陣列的索引標籤</param>
        /// <returns>回傳測試完的結果 0:未測試, 1:PASS, 2:fail, 3:error </returns>
        public int RunTest(int idx)
        {
            lblStatus.Text = string.Empty;
            int RunTest_result = 0; // 0:未測試,1:PASS,2:fail,3:error
            try
            {
                int i;
                string[] line;
                DialogResult dr;
                string[] cmd;
                string time1, time2, timeTmp;
                int j;
                FileStream fs;
                StreamWriter sw;
                double duration;
                string filePath, fileDirectory;
                int secs;
                //telnet = new TcpClient();

                lblFunction[idx].BackColor = Color.FromArgb(0, 255, 255);   // 測試中

                cmd = Convert.ToString(lblFunction[idx].Tag).Split(' ');
                if (cmd[0].ToUpper() != "CONSOLE-DUT" & cmd[0].ToUpper() != "CONSOLE-EUT" & cmd[0].ToUpper() != "TELNET" & cmd[0].ToUpper() != "POWER")
                {
                    if (!serialPort1.IsOpen & !telnet.Connected)
                    {
                        lblStatus.Text = "Console-DUT 或 Telnet 未連接";
                        return RunTest_result = 3;
                    }
                    if ((cmd[0].ToUpper() == "COMTOCOM" || cmd[0].ToUpper() == "CANTOCAN") & !serialPort2.IsOpen)
                    {
                        lblStatus.Text = "Console-EUT 未連接";
                        return RunTest_result = 3;
                    }
                }
                // for excel log
                idx_funlog = -1;
                if (cmd.GetUpperBound(0) < 2)
                {
                    if (TEST_FunLog.Contains(cmd[0].ToUpper()))
                    {
                        idx_funlog = TEST_FunLog.IndexOf(cmd[0].ToUpper());
                    }
                }
                else if (cmd.GetUpperBound(0) >= 2) // COMTOCOM(mode)
                {
                    if (TEST_FunLog.Contains(cmd[0].ToUpper() + "(" + cmd[2].ToUpper() + ")"))
                    {
                        idx_funlog = TEST_FunLog.IndexOf(cmd[0].ToUpper() + "(" + cmd[2].ToUpper() + ")");
                    }
                    else
                    {
                        idx_funlog = TEST_FunLog.IndexOf(cmd[0].ToUpper());
                    }
                }

                //SendCmd(string.Empty);
                switch (cmd[0].ToUpper())
                {
                    case "CONSOLE-DUT":     // Console show
                        txt_Rx.Text = string.Empty;
                        serialPort1_Close();
                        serialPort1.PortName = cmbDutCom.Text;
                        serialPort1.BaudRate = 115200;
                        serialPort1.Parity = Parity.None;
                        serialPort1.DataBits = 8;
                        serialPort1.StopBits = StopBits.One;
                        serialPort1.Handshake = Handshake.None; // 流量控制；交握協定
                        serialPort1.Open();
                        //serialPort1.DataReceived += new SerialDataReceivedEventHandler(serialPort1_DataReceived);
                        lblStatus.Text = "Console-DUT Connect OK !";
                        RunTest_result = 1;
                        if (cmd.GetUpperBound(0) >= 1)
                        {
                            if (cmd[1].ToUpper() == "SHOW")
                            {
                                consoleToolStripMenuItem.Checked = true;
                            }
                        }
                        else { consoleToolStripMenuItem.Checked = false; }

                        serialPort1.Write(((char)13).ToString());
                        Hold(100);
                        line = txt_Rx.Text.Split('\r');
                        for (i = line.GetUpperBound(0); i >= 0; i--)    // 從尾巴先搜尋
                        {
                            if (line[i].Contains("login"))
                            {
                                serialPort1.Write(USR + ((char)13).ToString());
                                Hold(200);
                                serialPort1.Write(PWD + ((char)13).ToString());
                                Hold(200);
                                serialPort1.Write("ifconfig eth0 " + TARGET_IP + ((char)13).ToString());
                                //Shell(appPATH, "arp-d.bat");
                                //Hold(1000);
                                break;  // for
                            }
                            else if (line[i].Contains("#"))
                            {
                                serialPort1.Write("ifconfig eth0 " + TARGET_IP + ((char)13).ToString());
                                //Shell(appPATH, "arp-d.bat");
                                //Hold(1000);
                                break;  // for
                            }
                            else if (line[i].Contains("Manufactory Settings"))
                            {
                                break;  // for
                            }
                        }

                        終端機指令.Enabled = true;
                        break;
                    case "CONSOLE-EUT":     // Console Port
                        serialPort2_Close();
                        serialPort2.PortName = cmbEutCom.Text;
                        serialPort2.BaudRate = 115200;
                        serialPort2.Parity = Parity.None;
                        serialPort2.DataBits = 8;
                        serialPort2.StopBits = StopBits.One;
                        serialPort2.Handshake = Handshake.None; // 流量控制；交握協定
                        serialPort2.Open();
                        serialPort2.DataReceived += new SerialDataReceivedEventHandler(serialPort2_DataReceived);
                        lblStatus.Text = "Console-EUT Connect OK !";
                        RunTest_result = 1;
                        serialPort2.Write("root" + ((char)13).ToString());
                        Hold(200);
                        serialPort2.Write("root" + ((char)13).ToString());
                        break;
                    case "TELNET":      // Telnet USR PWD
                        //Shell(appPATH, "arp-d.bat");
                        //Hold(1000);
                        //txt_Rx.Text = string.Empty;
                        RunTest_result = 1;
                        //if (telnet.Connected) { telnet.Close(); }
                        if (objping.Send(TARGET_IP, 1000).Status == System.Net.NetworkInformation.IPStatus.Success)
                        {
                            if (!telnet.Connected)
                            {
                                telnet = new TcpClient();
                                telnet.Connect(TARGET_IP, 23);   // 連接23端口 (Telnet的默認端口)
                                telentStream = telnet.GetStream();  // 建立網路資料流，將字串寫入串流

                                if (telnet.Connected)
                                {
                                    //lblStatus.Text = "連線成功，正在登錄...";
                                    lblStatus.Text = "正在登錄...";
                                    Hold(1000);
                                    // 背景telnet接收執行續
                                    if (rcvThread == null || !rcvThread.IsAlive)
                                    {
                                        ThreadStart backgroundReceive = new ThreadStart(telnet_Receive);
                                        rcvThread = new Thread(backgroundReceive);
                                        rcvThread.IsBackground = true;
                                        rcvThread.Start();
                                    }
                                    bytWrite_telnet = System.Text.Encoding.Default.GetBytes(USR + ((char)13).ToString());
                                    telentStream.Write(bytWrite_telnet, 0, bytWrite_telnet.Length);
                                    Hold(200);
                                    bytWrite_telnet = System.Text.Encoding.Default.GetBytes(PWD + ((char)13).ToString());
                                    telentStream.Write(bytWrite_telnet, 0, bytWrite_telnet.Length);
                                    lblStatus.Text = "連線成功 ";
                                }
                            }
                        }
                        else
                        {
                            lblStatus.Text = "ping失敗，請確認你的IP設定或網路設定";
                            RecNote(idx, cmd[0].ToUpper() + " Test Fail.");
                            RunTest_result = 2;
                        }
                        break;
                    case "TESTD":   // Testd LAN2ip
                        RunTest_result = 1;
                        if (atoptestd.Connected)
                        {
                            atoptestd.Close();
                            Hold(10000);
                        }
                        SendCmd("killall atop_testd");
                        Hold(100);
                        if (!atoptestd.Connected)
                        {
                            SendCmd("/jffs2/user.sh");
                            Hold(500);
                            Shell(appPATH, "arp-d.bat");
                            Hold(1000);
                            //txt_Rx.Text = string.Empty;
                            //if (telnet.Connected) { telnet.Close(); }
                            if (objping.Send(cmd[1], 1000).Status == System.Net.NetworkInformation.IPStatus.Success)
                            {
                                if (!atoptestd.Connected)
                                {
                                    atoptestd = new TcpClient();
                                    atoptestd.Connect(cmd[1], 4660);
                                    atoptestdStream = atoptestd.GetStream();  // 建立網路資料流，將字串寫入串流

                                    if (atoptestd.Connected)
                                    {
                                        //lblStatus.Text = "連線成功，正在登錄...";
                                        Hold(1000);
                                        // 背景atoptestd接收執行續
                                        ThreadStart backgroundReceive2 = new ThreadStart(atoptestd_Receive);
                                        Thread rcvThread2 = new Thread(backgroundReceive2);
                                        rcvThread2.IsBackground = true;
                                        rcvThread2.Start();

                                        lblStatus.Text = "連線成功 ";
                                    }
                                }
                            }
                            else
                            {
                                lblStatus.Text = "ping " + cmd[1] + "失敗，請確認atop_testd有無載入成功";
                                RecNote(idx, cmd[0].ToUpper() + " Test Fail.");
                                RunTest_result = 2;
                            }
                        }
                        break;
                    case "RESTART": // Restart login/none 開機時間(s)
                        if (telnet.Connected) { telnet.Close(); }
                        RunTest_result = 3;
                        SendCmd("restart&");
                        SendCmd("atop_restart&");
                        RecNote(idx, "Restart");
                        if (cmd[1].ToUpper() == "LOGIN")
                        {
                            secs = Convert.ToInt32(cmd[2]);
                            RunTest_result = ReCntTelnet(secs);   // need to login
                        }
                        else if (cmd[1].ToUpper() == "NONE")
                        {
                            secs = Convert.ToInt32(cmd[2]) * 1000;
                            if (MODEL_NAME.Contains("SE7416"))
                            {
                                WaitKey = "Enter 'help' for a list of built-in commands.";     // doesn't need to login
                                Hold(secs);
                                RunTest_result = 1;
                            }
                            else
                            {
                                WaitKey = "#";     // doesn't need to login
                                if (Hold(secs))
                                {
                                    RunTest_result = 1;
                                }
                            }
                        }
                        break;
                    case "RTC":
                        //SendCmd("killall date_adjust");
                        //Hold(1000);
                        time = DateTime.Now;
                        time1 = String.Format("{0:00}/{1:00}/{2:00}-{3:00}:{4:00}:{5:00}", time.Year, time.Month, time.Day, time.Hour, time.Minute, time.Second);
                        timeTmp = time1.Substring(0, time1.IndexOf("-"));
                        SendCmd("set_rtc " + time1);
                        Hold(100);
                        SendCmd("get_rtc");
                        Hold(300);
                        RunTest_result = 3;
                        line = txt_Rx.Text.Split('\r');
                        for (i = line.GetUpperBound(0); i >= 0; i--)    // 從尾巴先搜尋
                        {
                            if (line[i].Contains("get_rtc"))
                            {
                                if (line[i + 1].Contains(timeTmp))
                                {
                                    RunTest_result = 1;
                                    lblStatus.Text = cmd[0].ToUpper() + " Test Pass !";
                                }
                                else
                                {
                                    RunTest_result = 2;
                                    lblStatus.Text = cmd[0].ToUpper() + " Test Fail !";
                                    RecNote(idx, cmd[0].ToUpper() + " Test Fail.");
                                }
                                break;  // for
                            }
                        }
                        break;
                    case "GETRTC":  // GetRTC 開機時間(s)
                        if (MessageBox.Show("解除GPS治具，避免訊號干擾測試結果 ! \n 注意! 請斷電，等待 5 秒鐘再接回電源。 再按確定 ! ", "RTC 電池與底座 / 電容 Test", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) == DialogResult.Cancel)
                        {
                            return RunTest_result = 0;
                        }
                        for (j = 5; j >= 0; j--)
                        {
                            lblStatus.Text = "等待 " + j + " 秒";
                            Hold(1000);
                            if (j == 0)
                            {
                                lblStatus.Text = "請接回電源";
                                MessageBox.Show("請接回電源 ! ", "", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                            }
                        }
                        secs = Convert.ToInt32(cmd[1]);
                        ReCntTelnet(secs);
                        SendCmd("");
                        Hold(100);
                        //SendCmd("killall date_adjust");
                        //Hold(100);
                        SendCmd("get_rtc");
                        Hold(300);
                        time = DateTime.Now;
                        time2 = String.Format("{0:00}/{1:00}/{2:00} {3:00}:{4:00}:{5:00}", time.Year, time.Month, time.Day, time.Hour, time.Minute, time.Second);
                        timeTmp = time2.Substring(0, time2.IndexOf(" "));
                        RunTest_result = 3;
                        line = txt_Rx.Text.Split('\r');
                        for (i = line.GetUpperBound(0); i >= 0; i--)
                        {
                            if (line[i].Contains("get_rtc"))
                            {
                                if (line[i + 1].Contains(timeTmp))
                                {
                                    DateTime t1 = Convert.ToDateTime(line[i + 1]);
                                    DateTime t2 = Convert.ToDateTime(time2);
                                    int result = DateTime.Compare(t1, t2);
                                    if (result <= 0)    // 小於:t1早於t2、等於:t1等於t2
                                    {
                                        RunTest_result = 1;
                                        lblStatus.Text = cmd[0].ToUpper() + " Test Pass !";
                                    }
                                    else
                                    {
                                        RunTest_result = 2;
                                        lblStatus.Text = cmd[0].ToUpper() + " Test Fail !";
                                        RecNote(idx, cmd[0].ToUpper() + " Test Fail.");
                                    }
                                }
                                else
                                {
                                    RunTest_result = 2;
                                    lblStatus.Text = cmd[0].ToUpper() + " Test Fail !";
                                    RecNote(idx, cmd[0].ToUpper() + " Test Fail.");
                                }
                                break;  // for
                            }
                        }
                        break;
                    case "RS485":

                        break;
                    case "RS232":

                        break;
                    case "COM": // COM max_port mode BaudRate min
                        RunTest_result = 1;
                        for (j = 1; j <= Convert.ToInt32(cmd[1]); j = j + 2)
                        {
                            SendCmd(cmd[2].ToLower() + "_loopback " + j + " " + (j + 1));
                            WaitKey = "test ok";
                            if (Hold(3000) == false)
                            {
                                RunTest_result = 2;
                                SendCmd(((char)3).ToString());
                                string failmessage = "COM" + j + " -> COM" + (j + 1) + " Fail";
                                RecNote(idx, failmessage);
                            }
                            SendCmd(cmd[2].ToLower() + "_loopback " + (j + 1) + " " + j);
                            WaitKey = "test ok";
                            if (Hold(3000) == false)
                            {
                                RunTest_result = 2;
                                SendCmd(((char)3).ToString());
                                string failmessage = "COM" + (j + 1) + " -> COM" + j + " Fail";
                                RecNote(idx, failmessage);
                            }
                        }
                        if (RunTest_result == 1)
                        {
                            lblStatus.Text = "COM loopback Test Pass.";
                        }
                        break;
                    case "COMTOCOM":    // COMtoCOM port(1-4 or 4 or 0-4) mode time unit
                        string mode = cmd[2].Remove(0, 2);

                        if (chkHumanSkip.CheckState == CheckState.Checked)
                        {
                            lblStatus.Text = "略過更換治具的確認訊息";
                        }
                        else if (MessageBox.Show("請更換 " + cmd[2].ToUpper() + " 治具 !  ", cmd[2].ToUpper() + " test", MessageBoxButtons.OKCancel, MessageBoxIcon.Asterisk, MessageBoxDefaultButton.Button1) == DialogResult.Cancel)
                        {
                            return RunTest_result = 0;
                        }

                        if (MODEL_NAME.Contains("SE7816-512M") || MODEL_NAME.Contains("SE7816COM_512M") || MODEL_NAME.Contains("SE7116"))
                        //|| (MODEL_NAME.Contains("SE1416COM") & model_name.Contains("SE7816-512M")))
                        {
                            serialPort2.Write("cd /jffs2/" + ((char)13).ToString());
                            Hold(100);
                            serialPort2.Write("chmod 755 *" + ((char)13).ToString());
                            Hold(100);
                            serialPort2.Write("killall atop_tcp_server" + ((char)13).ToString());
                            Hold(100);
                            serialPort2.Write("killall tcp_server485_512m" + ((char)13).ToString());
                            Hold(100);
                            serialPort2.Write("killall tcp_server232_512m" + ((char)13).ToString());
                            Hold(100);
                            serialPort2.Write("killall tcp_server422_512m" + ((char)13).ToString());
                            Hold(100);
                            serialPort2.Write("./tcp_server" + mode + "_512m" + " 115200 &" + ((char)13).ToString());
                            Hold(100);
                            SendCmd("cd /jffs2/");
                            Hold(100);
                            //SendCmd("chmod 755 *");
                            //Hold(100);
                            SendCmd("killall atop_tcp_server");
                            Hold(100);
                            SendCmd("killall tcp_server485_512m");
                            Hold(100);
                            SendCmd("killall tcp_server232_512m");
                            Hold(100);
                            SendCmd("killall tcp_server422_512m");
                            Hold(100);
                            SendCmd("./tcp_server" + mode + "_512m" + " 115200 &");
                            Hold(100);
                        }
                        else if (MODEL_NAME.Contains("SE7816-256M") || MODEL_NAME.Contains("SE7816COM_256M"))
                        {
                            serialPort2.Write("cd /jffs2/" + ((char)13).ToString());
                            Hold(100);
                            serialPort2.Write("chmod 755 *" + ((char)13).ToString());
                            Hold(100);
                            serialPort2.Write("killall atop_tcp_server" + ((char)13).ToString());
                            Hold(100);
                            serialPort2.Write("killall tcp_server485_256m" + ((char)13).ToString());
                            Hold(100);
                            serialPort2.Write("killall tcp_server232_256m" + ((char)13).ToString());
                            Hold(100);
                            serialPort2.Write("killall tcp_server422_256m" + ((char)13).ToString());
                            Hold(100);
                            serialPort2.Write("./tcp_server" + mode + "_256m" + " 115200 &" + ((char)13).ToString());
                            Hold(100);
                            SendCmd("cd /jffs2/");
                            Hold(100);
                            //SendCmd("chmod 755 *");
                            //Hold(100);
                            SendCmd("killall atop_tcp_server");
                            Hold(100);
                            SendCmd("killall tcp_server485_256m");
                            Hold(100);
                            SendCmd("killall tcp_server232_256m");
                            Hold(100);
                            SendCmd("killall tcp_server422_256m");
                            Hold(100);
                            SendCmd("./tcp_server" + mode + "_256m" + " 115200 &");
                            Hold(100);
                        }
                        else if (MODEL_NAME.Contains("SE7416") || MODEL_NAME.Contains("SE1416COM_SE7416"))
                        {
                            serialPort2.Write("cd /jffs2/" + ((char)13).ToString());
                            Hold(100);
                            serialPort2.Write("chmod 755 *" + ((char)13).ToString());
                            Hold(100);
                            serialPort2.Write("killall atop_tcp_server" + ((char)13).ToString());
                            Hold(100);
                            serialPort2.Write("atop_tcp_server " + cmd[2] + " 115200 &" + ((char)13).ToString());
                            Hold(100);
                            SendCmd("cd /jffs2/");
                            Hold(100);
                            SendCmd("killall tcp_server");
                            Hold(100);
                            if (atoptestd.Connected)
                            {
                                atoptestd.Close();
                                Hold(10000);
                            }
                            SendCmd("killall atop_testd");
                            Hold(100);
                            SendCmd("./tcp_server " + cmd[2] + " 115200 &");
                            Hold(100);
                        }
                        else if (MODEL_NAME.Contains("SE1416COM_512M"))
                        {
                            serialPort2.Write("killall atop_tcp_server" + ((char)13).ToString());
                            Hold(100);
                            serialPort2.Write("atop_tcp_server " + cmd[2] + " 115200 &" + ((char)13).ToString());
                            Hold(100);
                            SendCmd("killall atop_tcp_server");
                            Hold(100);
                            SendCmd("atop_tcp_server " + cmd[2] + " 115200 &");
                            Hold(100);
                        }
                        else
                        {
                            MessageBox.Show("程式無法支援文字檔裡面所設定的 Model name !   ", "", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return RunTest_result = 3;
                        }

                        RunTest_result = 2;
                        // 建立檔案
                        fs = File.Open("Auto_Test", FileMode.OpenOrCreate, FileAccess.Write);
                        // 建構StreamWriter物件
                        sw = new StreamWriter(fs);
                        sw.Close();
                        fs.Close();
                        duration = Math.Round(TimeUnit(idx, 3) / 60, 2);
                        if (cmd[2].ToUpper() == "RS232")
                        {
                            // 因為SE7816 COM v1.1 的rs232有經過繼電器切換，所以interval太快，繼電器慢，容易造成資料loss
                            MultiPortTesting_settings(TARGET_eutIP, 3000, cmd[1], 4660, 1, duration.ToString());      // 3000只能負荷到16 port...需調整到能負擔32 port
                            COM_PID[0] = Shell(appPATH, "Multi-Port-Testingv1.6r.exe"); // Multi-Port Testing V1.6r (2014/9/25)
                            Hold(1000);
                            MultiPortTesting_settings(TARGET_IP, 3000, cmd[1], 4660, 0, duration.ToString());
                            COM_PID[1] = Shell(appPATH, "Multi-Port-Testingv1.6r.exe");
                        }
                        else
                        {
                            MultiPortTesting_settings(TARGET_eutIP, 1000, cmd[1], 4660, 1, duration.ToString());
                            COM_PID[0] = Shell(appPATH, "Multi-Port-Testingv1.6r.exe"); // Multi-Port Testing V1.6r (2014/9/25)
                            Hold(1000);
                            MultiPortTesting_settings(TARGET_IP, 1000, cmd[1], 4660, 0, duration.ToString());
                            COM_PID[1] = Shell(appPATH, "Multi-Port-Testingv1.6r.exe");
                        }
                        pause(duration);
                        if (File.Exists("Auto_Test"))
                        {
                            File.Delete("Auto_Test");
                        }
                        Hold(3000); // 因為Multi-Port-Testing 要產生結果(debug.txt文件)，所以等待是必須的
                        CloseShell(COM_PID[0]);
                        CloseShell(COM_PID[1]);
                        COM_PID[0] = 0;
                        COM_PID[1] = 0;
                        if (!File.Exists("debug.txt"))
                        {
                            RunTest_result = 1;
                        }

                        if (MODEL_NAME.Contains("SE7816-512M") || MODEL_NAME.Contains("SE7816COM_512M") || MODEL_NAME.Contains("SE7116"))
                        {
                            serialPort2.Write("killall tcp_server" + mode + "_512m" + ((char)13).ToString());
                            Hold(100);
                            SendCmd("killall tcp_server" + mode + "_512m");
                            Hold(100);
                        }
                        else if (MODEL_NAME.Contains("SE7816-256M") || MODEL_NAME.Contains("SE7816COM_256M"))
                        {
                            serialPort2.Write("killall tcp_server" + mode + "_256m" + ((char)13).ToString());
                            Hold(100);
                            SendCmd("killall tcp_server" + mode + "_256m");
                            Hold(100);
                        }
                        else if (MODEL_NAME.Contains("SE7416") || MODEL_NAME.Contains("SE1416COM_SE7416"))
                        {
                            serialPort2.Write("killall atop_tcp_server" + ((char)13).ToString());
                            Hold(100);
                            SendCmd("killall tcp_server");
                            Hold(100);
                        }
                        else if (MODEL_NAME.Contains("SE1416COM_512M"))
                        {
                            serialPort2.Write("killall atop_tcp_server" + ((char)13).ToString());
                            Hold(100);
                            SendCmd("killall atop_tcp_server");
                            Hold(100);
                        }

                        // 把關沒有產生debug.txt文件的其他error
                        line = txt_Rx.Text.Split('\r');
                        for (i = line.GetUpperBound(0); i >= 0; i--)    // 從尾巴先搜尋
                        {
                            if (line[i].Contains("Terminated")) { break; }
                            if (line[i].Contains("error") || line[i].Contains("No such file or directory") || line[i].Contains("not found"))
                            {
                                RunTest_result = 2;
                                break;  // for
                            }
                        }

                        if (RunTest_result == 1)
                        {
                            lblStatus.Text = cmd[0].ToUpper() + " Test Pass.";
                        }
                        else
                        {
                            RecNote(idx, cmd[0].ToUpper() + " Test Fail.");
                        }
                        break;
                    case "CANTOCAN": // CAN port(1-2 or 2 or 0-2) time unit
                        serialPort2.Write("killall " + CAN_function + ((char)13).ToString());
                        Hold(100);
                        serialPort2.Write(CAN_function + " &" + ((char)13).ToString());
                        Hold(100);
                        SendCmd("killall " + CAN_function);
                        Hold(100);
                        SendCmd(CAN_function + " &");
                        Hold(100);
                        RunTest_result = 2;
                        // 建立檔案
                        fs = File.Open("Auto_Test", FileMode.OpenOrCreate, FileAccess.Write);
                        // 建構StreamWriter物件
                        sw = new StreamWriter(fs);
                        sw.Close();
                        fs.Close();
                        duration = Math.Round(TimeUnit(idx, 2) / 60, 2);
                        MultiPortTesting_settings(TARGET_eutIP, 1000, cmd[1], 8000, 1, duration.ToString());
                        COM_PID[0] = Shell(appPATH, "Multi-Port-Testingv1.6r.exe");
                        Hold(500);
                        MultiPortTesting_settings(TARGET_IP, 1000, cmd[1], 8000, 0, duration.ToString());
                        COM_PID[1] = Shell(appPATH, "Multi-Port-Testingv1.6r.exe");
                        pause(duration);
                        if (File.Exists("Auto_Test"))
                        {
                            File.Delete("Auto_Test");
                        }
                        Hold(3000); // 因為Multi-Port-Testingv1.6p (2013/11/22)要產生結果(debug.txt文件)，所以等待是必須的
                        CloseShell(COM_PID[0]);
                        CloseShell(COM_PID[1]);
                        COM_PID[0] = 0;
                        COM_PID[1] = 0;
                        if (!File.Exists("debug.txt"))
                        {
                            RunTest_result = 1;
                        }
                        // 把關沒有產生debug.txt文件的其他error
                        line = txt_Rx.Text.Split('\r');
                        for (i = line.GetUpperBound(0); i >= 0; i--)    // 從尾巴先搜尋
                        {
                            if (line[i].Contains("Terminated")) { break; }
                            if (line[i].Contains("error") || line[i].Contains("No such file or directory") || line[i].Contains("not found"))
                            {
                                RunTest_result = 2;
                                break;  // for
                            }
                        }
                        if (RunTest_result == 1)
                        {
                            lblStatus.Text = cmd[0].ToUpper() + " Test Pass.";
                        }
                        else
                        {
                            RecNote(idx, cmd[0].ToUpper() + " Test Fail.");
                        }
                        serialPort2.Write("killall " + CAN_function + ((char)13).ToString());
                        Hold(100);
                        SendCmd("killall " + CAN_function);
                        Hold(100);
                        break;
                    case "LOADTOOLS":   // "MODEL_NAME"_Tools資料夾裡的所有檔案載入待測物
                        // QC有遇到ifconfig ip更改後Restart後，ip又回到burn-in ip的流程問題~
                        if (objping.Send(TARGET_IP, 500).Status == System.Net.NetworkInformation.IPStatus.Success)
                        {
                            RunTest_result = 1;
                            fileDirectory = MODEL_NAME + "_Tools";
                            filePath = appPATH + "\\" + fileDirectory;
                            if (Directory.Exists(fileDirectory))
                            {
                                // Process the list of files found in the directory.
                                string[] fileEntries = Directory.GetFiles(filePath);
                                foreach (string fileName in fileEntries)
                                {
                                    string sourceFile = fileName.Replace(filePath + "\\", "");
                                    uploadFile(TARGET_IP, fileDirectory + "\\" + sourceFile, USR, PWD);
                                    Hold(1);
                                    bool check = checkFile(TARGET_IP, sourceFile, USR, PWD);
                                    if (!check) // false代表沒有上載成功，檔案不存在
                                    {
                                        RecNote(idx, sourceFile + " not exist!");
                                        RunTest_result = 2;
                                    }
                                }
                            }
                            SendCmd("chmod 755 /jffs2/*");
                            Hold(100);
                            if (MODEL_NAME.Contains("SE7816"))
                            {
                                SendCmd("mv /jffs2/restored_8377 /jffs2/restored");
                                Hold(100);
                            }
                            else if (MODEL_NAME.Contains("SE7416"))
                            {
                                SendCmd("mv /jffs2/restored_7416_idt /jffs2/restored");
                                Hold(100);
                                SendCmd("mv /jffs2/atop_testd_7416_idt /jffs2/atop_testd");
                                Hold(100);
                                SendCmd("mv /jffs2/idt_tcp_server /jffs2/tcp_server");
                                Hold(100);
                            }
                            else if (MODEL_NAME.Contains("SE7116"))
                            {
                                SendCmd("mv /jffs2/restored_8377 /jffs2/restored");
                                Hold(100);
                            }
                            SendCmd("ls -al /jffs2/");
                        }
                        break;
                    case "FACTORYFILES":
                        SendCmd("ifconfig eth0 " + TARGET_IP);
                        Hold(1000);
                        if (objping.Send(TARGET_IP, 500).Status == System.Net.NetworkInformation.IPStatus.Success)
                        {
                            RunTest_result = 1;
                            fileDirectory = MODEL_NAME + "_factoryfiles";
                            filePath = appPATH + "\\" + fileDirectory;
                            if (Directory.Exists(fileDirectory))
                            {
                                // Process the list of files found in the directory.
                                string[] fileEntries = Directory.GetFiles(filePath);
                                foreach (string fileName in fileEntries)
                                {
                                    string sourceFile = fileName.Replace(filePath + "\\", "");
                                    uploadFile(TARGET_IP, fileDirectory + "\\" + sourceFile, USR, PWD);
                                    Hold(1);
                                    bool check = checkFile(TARGET_IP, sourceFile, USR, PWD);
                                    if (!check) // false代表沒有上載成功，檔案不存在
                                    {
                                        RecNote(idx, sourceFile + " not exist!");
                                        RunTest_result = 2;
                                    }
                                }
                            }
                            SendCmd("chmod 755 /jffs2/*");
                            Hold(100);
                            if (MODEL_NAME.Contains("SE7816-512M") || MODEL_NAME.Contains("SE7116"))
                            {
                                SendCmd("mv /jffs2/tcp_server232_powerpc /jffs2/atop_tcp_server232512110808");
                                Hold(100);
                                SendCmd("mv /jffs2/tcp_server422_powerpc /jffs2/atop_tcp_server422512110808");
                                Hold(100);
                                SendCmd("mv /jffs2/tcp_server485_powerpc /jffs2/atop_tcp_server485512110808");
                                Hold(100);
                            }
                            else if (MODEL_NAME.Contains("SE7816-256M"))
                            {
                                SendCmd("mv /jffs2/tcp_server232_powerpc /jffs2/tcp_server232");
                                Hold(100);
                                SendCmd("mv /jffs2/tcp_server422_powerpc /jffs2/tcp_server422");
                                Hold(100);
                                SendCmd("mv /jffs2/tcp_server485_powerpc /jffs2/tcp_server485");
                                Hold(100);
                            }
                            else if (MODEL_NAME.Contains("SE7416"))
                            {
                                SendCmd("mv /jffs2/tcp_server232_idt /jffs2/tcp_server232");
                                Hold(100);
                                SendCmd("mv /jffs2/tcp_server422_idt /jffs2/tcp_server422");
                                Hold(100);
                                SendCmd("mv /jffs2/tcp_server485_idt /jffs2/tcp_server485");
                                Hold(100);
                            }
                            SendCmd("ls -al /jffs2/");
                        }
                        break;
                    case "DELETE":
                        if (atoptestd.Connected)
                        {
                            atoptestd.Close();
                            Hold(7000);
                        }
                        SendCmd("rm /jffs2/*");
                        RunTest_result = 1;
                        //fileDirectory = MODEL_NAME + "_Tools";
                        //filePath = appPATH + "\\" + fileDirectory;
                        //if (Directory.Exists(fileDirectory))
                        //{
                        //    // Process the list of files found in the directory.
                        //    string[] fileEntries = Directory.GetFiles(filePath);
                        //    foreach (string fileName in fileEntries)
                        //    {
                        //        string sourceFile = fileName.Replace(filePath + "\\", "");
                        //        bool check = checkFile(TARGET_IP, sourceFile, USR, PWD);
                        //        if (check)  // true代表沒有刪除成功
                        //        {
                        //            RecNote(idx, sourceFile + " 沒有刪除成功!");
                        //            RunTest_result = 2;
                        //        }
                        //    }
                        //}
                        SendCmd("rm /jffs2/*");
                        Hold(100);
                        SendCmd("killall atop_testd");
                        Hold(100);
                        SendCmd("ls /jffs2/");
                        break;
                    case "LAN":
                        SendCmd("LAN end search");
                        MessageBox.Show(" 請插拔網路線 !", cmd[0] + " test", MessageBoxButtons.OK, MessageBoxIcon.None);
                        string lan1, lan2, lan3, lan4, lan5, lan6, lan7, lan8;
                        lan1 = "Lan1->Fail";
                        lan2 = "Lan2->Fail";
                        lan3 = "Lan3->Fail";
                        lan4 = "Lan4->Fail";
                        lan5 = "Lan5->Fail";
                        lan6 = "Lan6->Fail";
                        lan7 = "Lan7->Fail";
                        lan8 = "Lan8->Fail";
                        RunTest_result = 2;
                        Hold(3000);
                        line = txt_Rx.Text.Split('\r');
                        if (MODEL_NAME.Contains("SE7816"))
                        {
                            for (i = line.GetUpperBound(0); i >= 0; i--)
                            {
                                if (line[i].Contains("LAN end search"))
                                {
                                    break;
                                }
                                // lan 1
                                if (line[i].Contains("e0024520:02 - Link is Up - 1000/Full"))
                                {
                                    lan1 = "Lan1->Ok";
                                }
                                // lan 2
                                else if (line[i].Contains("e0024520:03 - Link is Up - 1000/Full"))
                                {
                                    lan2 = "Lan2->Ok";
                                }
                                // lab 3
                                else if (line[i].Contains("eth2: link up"))
                                {
                                    lan3 = "Lan3->Ok";
                                }
                                // lan 4
                                else if (line[i].Contains("eth3: link up"))
                                {
                                    lan4 = "Lan4->Ok";
                                }
                                // lan 5
                                else if (line[i].Contains("eth4: link up"))
                                {
                                    lan5 = "Lan5->Ok";
                                }
                                // lan 6
                                else if (line[i].Contains("eth5: link up"))
                                {
                                    lan6 = "Lan6->Ok";
                                }
                            }
                            if (lan1.Contains("Ok") && lan2.Contains("Ok") && lan3.Contains("Ok") && lan4.Contains("Ok") && lan5.Contains("Ok") && lan6.Contains("Ok"))
                            {
                                lblStatus.Text = cmd[0].ToUpper() + " Test Pass.";
                                RunTest_result = 1;
                            }
                            else
                            {
                                lblStatus.Text = cmd[0].ToUpper() + " Test Fail !";
                                RecNote(idx, lan1 + " , " + lan2 + " , " + lan3 + " , " + lan4 + " , " + lan5 + " , " + lan6);
                            }
                        }
                        else if (MODEL_NAME.Contains("SE7116"))
                        {
                            for (i = line.GetUpperBound(0); i >= 0; i--)
                            {
                                if (line[i].Contains("LAN end search"))
                                {
                                    break;
                                }
                                // lan 1
                                if (line[i].Contains("ffe24000:02 - Link is Up - 1000/Full"))
                                {
                                    lan1 = "Lan1->Ok";
                                }
                                // lan 2
                                else if (line[i].Contains("ffe24000:03 - Link is Up - 1000/Full"))
                                {
                                    lan2 = "Lan2->Ok";
                                }
                                // lab 3
                                else if (line[i].Contains("eth2: Link is Up - 1000/Full"))
                                {
                                    lan3 = "Lan3->Ok";
                                }
                                // lan 4
                                else if (line[i].Contains("eth3: Link is Up - 1000/Full"))
                                {
                                    lan4 = "Lan4->Ok";
                                }
                                // lan 5
                                else if (line[i].Contains("eth4: Link is Up - 1000/Full"))
                                {
                                    lan5 = "Lan5->Ok";
                                }
                                // lan 6
                                else if (line[i].Contains("eth5: Link is Up - 1000/Full"))
                                {
                                    lan6 = "Lan6->Ok";
                                }
                                // lan 7
                                else if (line[i].Contains("eth6: Link is Up - 1000/Full"))
                                {
                                    lan7 = "Lan7->Ok";
                                }
                                // lan 8
                                else if (line[i].Contains("eth7: Link is Up - 1000/Full"))
                                {
                                    lan8 = "Lan8->Ok";
                                }
                            }
                            if (lan1.Contains("Ok") && lan2.Contains("Ok") && lan3.Contains("Ok") && lan4.Contains("Ok") && lan5.Contains("Ok") && lan6.Contains("Ok") && lan7.Contains("Ok") && lan8.Contains("Ok"))
                            {
                                lblStatus.Text = cmd[0].ToUpper() + " Test Pass.";
                                RunTest_result = 1;
                            }
                            else
                            {
                                lblStatus.Text = cmd[0].ToUpper() + " Test Fail !";
                                RecNote(idx, lan1 + " , " + lan2 + " , " + lan3 + " , " + lan4 + " , " + lan5 + " , " + lan6 + " , " + lan7 + " , " + lan8);
                            }
                        }
                        break;
                    case "DI":

                        break;
                    case "DO":  // relay
                        RunTest_result = 2;
                        if (cmd.GetUpperBound(0) >= 2)
                        {
                            if (cmd[2].ToUpper() == "SKIP")
                            {
                                lblStatus.Text = "略過人工判斷";
                                SendCmd("atop_do " + cmd[1] + " 1");
                                Hold(1000);
                                SendCmd("atop_do " + cmd[1] + " 0");
                                Hold(1000);
                                SendCmd("atop_do " + cmd[1] + " 1");
                                Hold(1000);
                                SendCmd("atop_do " + cmd[1] + " 0");
                                RunTest_result = 1;
                            }
                        }
                        else if (chkHumanSkip.CheckState == CheckState.Checked)
                        {
                            lblStatus.Text = "略過人工判斷";
                            SendCmd("atop_do " + cmd[1] + " 1");
                            Hold(1000);
                            SendCmd("atop_do " + cmd[1] + " 0");
                            Hold(1000);
                            SendCmd("atop_do " + cmd[1] + " 1");
                            Hold(1000);
                            SendCmd("atop_do " + cmd[1] + " 0");
                            RunTest_result = 1;
                        }
                        else
                        {
                            lblStatus.Text = "人工判斷";
                            SendCmd("atop_do " + cmd[1] + " 1");
                            Hold(1000);
                            SendCmd("atop_do " + cmd[1] + " 0");
                            Hold(1000);
                            SendCmd("atop_do " + cmd[1] + " 1");
                            Hold(1000);
                            SendCmd("atop_do " + cmd[1] + " 0");
                            dr = MessageBox.Show(" Relay 燈號是否正常 ? ", cmd[0] + " Test", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button1);
                            if (dr == DialogResult.Yes) { RunTest_result = 1; }
                            else if (dr == DialogResult.No) { RunTest_result = 2; }
                        }
                        break;
                    case "DOTODI":  // dio number
                        int number1, number2 = -1;
                        if (cmd[1].Contains("0")) { return RunTest_result = 3; } // 設定錯誤
                        if (cmd[1].Contains("-")) // 格式ex: 1、3-4
                        {
                            string[] numArray;
                            numArray = cmd[1].Split(new char[] { '-' });
                            number1 = Convert.ToInt32(numArray[0]);
                            number2 = Convert.ToInt32(numArray[1]);
                            if (Math.Abs(number1 - number2) != 1)
                            {
                                return RunTest_result = 3; // 設定錯誤
                            }
                        }
                        else
                        {
                            number1 = Convert.ToInt32(cmd[1]);    // 格式ex: 1
                        }
                        string di1_1 = string.Empty;
                        string di1_2 = string.Empty;
                        string di2_1 = string.Empty;
                        string di2_2 = string.Empty;
                        // test number1
                        SendCmd("atop_di " + number1);
                        Hold(500);
                        di1_1 = GetLine("DI" + number1, "atop_di");
                        SendCmd("atop_do " + number1 + " 1");
                        Hold(500);
                        SendCmd("atop_di " + number1);
                        Hold(500);
                        di1_2 = GetLine("DI" + number1, "atop_di");
                        SendCmd("atop_do " + number1 + " 0");
                        Hold(500);

                        // test number2
                        if (number2 != -1)
                        {
                            SendCmd("atop_di " + number2);
                            Hold(500);
                            di2_1 = GetLine("DI" + number2, "atop_di");
                            SendCmd("atop_do " + number2 + " 1");
                            Hold(500);
                            SendCmd("atop_di " + number2);
                            Hold(500);
                            di2_2 = GetLine("DI" + number2, "atop_di");
                            SendCmd("atop_do " + number2 + " 0");
                            Hold(500);
                        }
                        // 判斷DI狀態有改變就pass
                        if (number2 != -1)
                        {
                            lblStatus.Text = di1_1 + "->" + di1_2 + " , " + di2_1 + "->" + di2_2;
                            if (!di1_1.Equals(di1_2) || !di2_1.Equals(di2_2))
                            {
                                RunTest_result = 1;
                            }
                            else
                            {
                                RecNote(idx, di1_1 + "->" + di1_2 + " , " + di2_1 + "->" + di2_2);
                                RunTest_result = 2;
                            }
                        }
                        else
                        {
                            lblStatus.Text = di1_1 + "->" + di1_2;
                            if (!di1_1.Equals(di1_2))
                            {
                                RunTest_result = 1;
                            }
                            else
                            {
                                RecNote(idx, di1_1 + "->" + di1_2);
                                RunTest_result = 2;
                            }
                        }
                        break;
                    case "KEYPAD":
                        MessageBox.Show("按下確定鍵後，請在 5 秒內按壓 Keypad 任一按鍵。  ", cmd[0] + " test", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        SendCmd("atop_keypad");
                        RunTest_result = 2;
                        WaitKey = "0000";
                        if (Hold(5000))
                        {
                            RunTest_result = 1;
                            lblStatus.Text = cmd[0].ToUpper() + " Test Pass.";
                        }
                        SendCmd(((char)3).ToString()); // ((char)3):Ctrl+c
                        break;
                    case "LCM":
                        SendCmd("lcmd 0 &");
                        if (cmd.GetUpperBound(0) >= 1)
                        {
                            if (cmd[1].ToUpper() == "SKIP")
                            {
                                lblStatus.Text = "略過人工判斷";
                                Hold(1000);
                                RunTest_result = 1;
                            }
                        }
                        else if (chkHumanSkip.CheckState == CheckState.Checked)
                        {
                            lblStatus.Text = "略過人工判斷";
                            Hold(1000);
                            RunTest_result = 1;
                        }
                        else
                        {
                            lblStatus.Text = "人工判斷";
                            dr = MessageBox.Show("顯示器是否有印出數字 ? ", cmd[0] + "Test", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button1);
                            if (dr == DialogResult.Yes) { RunTest_result = 1; }
                            else if (dr == DialogResult.No) { RecNote(idx, cmd[0].ToUpper() + " Test Fail."); RunTest_result = 2; }
                        }
                        break;
                    case "EEPROM":  // for se190x at present
                        txt_Rx.Text = string.Empty;
                        SendCmd("atop_eeprom_util EEPROM 0 256");
                        Hold(1000);
                        if (txt_Rx.Text.Length >= 512 && txt_Rx.Text.Contains("ATOP"))
                        {
                            RunTest_result = 1;
                        }
                        else
                        {
                            RunTest_result = 2;
                        }
                        lblStatus.Text = "EEPROM Test Len = " + txt_Rx.Text.Length;
                        break;
                    case "EEPROMIC":
                        string eeprom_check1, eeprom_check2, eeprom_check3, eeprom_check4;
                        eeprom_check1 = "initialword";
                        eeprom_check2 = "initialword";
                        eeprom_check3 = "initialword";
                        eeprom_check4 = "initialword";
                        if (MODEL_NAME.Contains("512M"))
                        {
                            switch (cmd[1])
                            {
                                case "1":
                                    eeprom_check1 = "eth2: RealTek RTL8139 at 0xe1096000, 00:e0:4c:77:11:a6, IRQ 21";
                                    eeprom_check2 = "eth3: RealTek RTL8139 at 0xe1098100, 00:e0:4c:77:11:a6, IRQ 21";
                                    eeprom_check3 = "eth4: RealTek RTL8139 at 0xe109a200, 00:e0:4c:77:11:a6, IRQ 21";
                                    eeprom_check4 = "eth5: RealTek RTL8139 at 0xe109c300, 00:e0:4c:77:11:a6, IRQ 21";
                                    break;
                                case "2":
                                    eeprom_check1 = "eth2: RealTek RTL8139 at 0xe10a6000, 00:e0:4c:77:11:a6, IRQ 21";
                                    eeprom_check2 = "eth3: RealTek RTL8139 at 0xe10a8100, 00:e0:4c:77:11:a6, IRQ 21";
                                    eeprom_check3 = "eth4: RealTek RTL8139 at 0xe10aa200, 00:e0:4c:77:11:a6, IRQ 21";
                                    eeprom_check4 = "eth5: RealTek RTL8139 at 0xe10ac300, 00:e0:4c:77:11:a6, IRQ 21";
                                    break;
                                case "3":
                                    eeprom_check1 = "eth2: RealTek RTL8139 at 0xe10b6000, 00:e0:4c:77:11:a6, IRQ 21";
                                    eeprom_check2 = "eth3: RealTek RTL8139 at 0xe10b8100, 00:e0:4c:77:11:a6, IRQ 21";
                                    eeprom_check3 = "eth4: RealTek RTL8139 at 0xe10ba200, 00:e0:4c:77:11:a6, IRQ 21";
                                    eeprom_check4 = "eth5: RealTek RTL8139 at 0xe10bc300, 00:e0:4c:77:11:a6, IRQ 21";
                                    break;
                                case "4":
                                    eeprom_check1 = "eth2: RealTek RTL8139 at 0xe10c6000, 00:e0:4c:77:11:a6, IRQ 21";
                                    eeprom_check2 = "eth3: RealTek RTL8139 at 0xe10c8100, 00:e0:4c:77:11:a6, IRQ 21";
                                    eeprom_check3 = "eth4: RealTek RTL8139 at 0xe10ca200, 00:e0:4c:77:11:a6, IRQ 21";
                                    eeprom_check4 = "eth5: RealTek RTL8139 at 0xe10cc300, 00:e0:4c:77:11:a6, IRQ 21";
                                    break;
                            }
                        }
                        else if (MODEL_NAME.Contains("256M"))
                        {
                            switch (cmd[1])
                            {
                                case "1":
                                    eeprom_check1 = "eth2: RealTek RTL8139 at 0xd1076000, 00:e0:4c:77:11:a6, IRQ 21";
                                    eeprom_check2 = "eth3: RealTek RTL8139 at 0xd1078100, 00:e0:4c:77:11:a6, IRQ 21";
                                    eeprom_check3 = "eth4: RealTek RTL8139 at 0xd107a200, 00:e0:4c:77:11:a6, IRQ 21";
                                    eeprom_check4 = "eth5: RealTek RTL8139 at 0xd107c300, 00:e0:4c:77:11:a6, IRQ 21";
                                    break;
                                case "2":
                                    eeprom_check1 = "eth2: RealTek RTL8139 at 0xd1086000, 00:e0:4c:77:11:a6, IRQ 21";
                                    eeprom_check2 = "eth3: RealTek RTL8139 at 0xd1088100, 00:e0:4c:77:11:a6, IRQ 21";
                                    eeprom_check3 = "eth4: RealTek RTL8139 at 0xd108a200, 00:e0:4c:77:11:a6, IRQ 21";
                                    eeprom_check4 = "eth5: RealTek RTL8139 at 0xd108c300, 00:e0:4c:77:11:a6, IRQ 21";
                                    break;
                                case "3":
                                    eeprom_check1 = "eth2: RealTek RTL8139 at 0xd1096000, 00:e0:4c:77:11:a6, IRQ 21";
                                    eeprom_check2 = "eth3: RealTek RTL8139 at 0xd1098100, 00:e0:4c:77:11:a6, IRQ 21";
                                    eeprom_check3 = "eth4: RealTek RTL8139 at 0xd109a200, 00:e0:4c:77:11:a6, IRQ 21";
                                    eeprom_check4 = "eth5: RealTek RTL8139 at 0xd109c300, 00:e0:4c:77:11:a6, IRQ 21";
                                    break;
                                case "4":
                                    eeprom_check1 = "eth2: RealTek RTL8139 at 0xd10a6000, 00:e0:4c:77:11:a6, IRQ 21";
                                    eeprom_check2 = "eth3: RealTek RTL8139 at 0xd10a8100, 00:e0:4c:77:11:a6, IRQ 21";
                                    eeprom_check3 = "eth4: RealTek RTL8139 at 0xd10aa200, 00:e0:4c:77:11:a6, IRQ 21";
                                    eeprom_check4 = "eth5: RealTek RTL8139 at 0xd10ac300, 00:e0:4c:77:11:a6, IRQ 21";
                                    break;
                            }
                        }
                        RunTest_result = 1;
                        if (!txt_Rx.Text.Contains(eeprom_check1)) { RunTest_result = 2; }
                        if (!txt_Rx.Text.Contains(eeprom_check2)) { RunTest_result = 2; }
                        if (!txt_Rx.Text.Contains(eeprom_check3)) { RunTest_result = 2; }
                        if (!txt_Rx.Text.Contains(eeprom_check4)) { RunTest_result = 2; }
                        if (RunTest_result == 1)
                        {
                            lblStatus.Text = cmd[0].ToUpper() + " Test Pass.";
                        }
                        else
                        {
                            RecNote(idx, cmd[0].ToUpper() + " Test Fail.");
                        }
                        break;
                    case "GPS":
                        SendCmd("atop_get_gpstime");
                        Hold(300);
                        line = txt_Rx.Text.Split('\r');
                        RunTest_result = 2;
                        for (i = line.GetUpperBound(0); i >= 0; i--)
                        {
                            if (line[i].Contains("SYNC OK"))
                            {
                                RunTest_result = 1;
                                break;  // for
                            }
                            if (line[i].Contains("atop_get_gpstime"))
                            {
                                break;  // for
                            }
                        }
                        if (RunTest_result == 1)
                        {
                            lblStatus.Text = cmd[0].ToUpper() + " Test Pass !";
                        }
                        else
                        {
                            RecNote(idx, cmd[0].ToUpper() + " Test Fail.");
                            lblStatus.Text = cmd[0].ToUpper() + " Test Fail !";
                        }
                        break;
                    case "LED":

                        break;
                    case "BUZZER":
                        if (MODEL_NAME.Contains("SE7416"))
                        {
                            if (SendTestd("rtc_test") == 3)
                            {
                                return RunTest_result = 3;
                            }
                            Hold(2000);
                        }
                        else
                        {
                            SendCmd("atop_buzzer");
                        }

                        if (cmd.GetUpperBound(0) >= 1)
                        {
                            if (cmd[1].ToUpper() == "SKIP")
                            {
                                lblStatus.Text = "略過人工判斷";
                                Hold(2000); // wait for buzzer
                                RunTest_result = 1;
                            }
                        }
                        else if (chkHumanSkip.CheckState == CheckState.Checked)
                        {
                            lblStatus.Text = "略過人工判斷";
                            Hold(2000); // wait for buzzer
                            RunTest_result = 1;
                        }
                        else
                        {
                            lblStatus.Text = "人工判斷";
                            Hold(1000);
                            dr = MessageBox.Show("是否有聽到蜂鳴器發出聲響 ? ", cmd[0] + "Test", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button1);
                            if (dr == DialogResult.Yes) { RunTest_result = 1; }
                            else if (dr == DialogResult.No) { RecNote(idx, cmd[0].ToUpper() + " Test Fail."); RunTest_result = 2; }
                        }
                        break;
                    case "FLASH":

                        break;
                    case "SD":
                        RunTest_result = 2;
                        SendCmd("cd /jffs2/");
                        Hold(100);
                        //SendCmd("chmod 755 *");
                        //Hold(100);
                        //SendCmd("sync");
                        //Hold(100);
                        // 檢測SD有無mount
                        SendCmd("df -h");
                        Hold(2000);
                        line = txt_Rx.Text.Split('\r');
                        for (i = line.GetUpperBound(0); i >= 0; i--)
                        {
                            if (line[i].Contains("/mnt/sd"))
                            {
                                SendCmd("cp " + SD_function + " /mnt/sd/");
                                Hold(500); // 讓檔案有足夠的時間能確實的被寫入到裝置
                                SendCmd("/mnt/sd/" + SD_function);
                                Hold(300);
                                break;  // for
                            }
                            if (line[i].Contains("df -h"))
                            {
                                break;  // for
                            }
                        }

                        line = txt_Rx.Text.Split('\r');
                        for (i = line.GetUpperBound(0); i >= 0; i--)
                        {
                            if (line[i].Contains("GET SD OK"))
                            {
                                RunTest_result = 1;
                                break;  // for
                            }
                            if (line[i].Contains("df -h"))
                            {
                                break;  // fail，所以搜尋到df -h就停止搜尋
                            }
                        }
                        if (RunTest_result == 1)
                        {
                            lblStatus.Text = cmd[0].ToUpper() + " Test Pass !";
                        }
                        else
                        {
                            RecNote(idx, cmd[0].ToUpper() + " Test Fail.");
                            lblStatus.Text = cmd[0].ToUpper() + " Test Fail !";
                        }
                        break;
                    case "SATA":
                        RunTest_result = 2;
                        SendCmd("cd /jffs2/");
                        Hold(100);
                        //SendCmd("chmod 755 *");
                        //Hold(100);
                        //SendCmd("sync");    // sync 指令會將存 buffer 中的資料強制寫入硬盤中
                        //Hold(100);
                        // 檢測SATA有無mount
                        SendCmd("df -h");
                        Hold(2000);
                        line = txt_Rx.Text.Split('\r');
                        for (i = line.GetUpperBound(0); i >= 0; i--)
                        {
                            if (line[i].Contains("/mnt/hdd0"))
                            {
                                SendCmd("cp " + SATA_function + " /mnt/hdd0/");
                                Hold(500); // 讓檔案有足夠的時間能確實的被寫入到裝置
                                SendCmd("/mnt/hdd0/" + SATA_function);
                                Hold(300);
                                break;
                            }
                            if (line[i].Contains("df -h"))
                            {
                                break;  // for
                            }
                        }

                        line = txt_Rx.Text.Split('\r');
                        for (i = line.GetUpperBound(0); i >= 0; i--)
                        {
                            if (line[i].Contains("GET SATA OK"))
                            {
                                RunTest_result = 1;
                                break;  // for
                            }
                            if (line[i].Contains("df -h"))
                            {
                                break;  // fail，所以搜尋到df -h就停止搜尋
                            }
                        }
                        if (RunTest_result == 1)
                        {
                            lblStatus.Text = cmd[0].ToUpper() + " Test Pass !";
                        }
                        else
                        {
                            RecNote(idx, cmd[0].ToUpper() + " Test Fail.");
                            lblStatus.Text = cmd[0].ToUpper() + " Test Fail !";
                        }
                        break;
                    case "USB":
                        /* mount /dev/sda1 /mnt/usb (mount -t type device dir)
                             Q:為什麼是/dev/sda1
                             A:因為usb是模擬成scsi storage裝置
                             Q:如果mount不成功，那有可能的問題是什麼
                             A:1.請注意/dev/sda1 /mnt/usb 檔案路徑是否存在
                             2.請注意是否要加上指定檔案系統格式 -t vfat (參考/proc/filesystem)
                             3.如果不是/dev/sda1 sda2 sda3 ..... 那就只好看dmesg 訊息來得知device name或者用fdisk -l 也行
                        */
                        RunTest_result = 2;
                        if (MODEL_NAME.Contains("SE7816-512M") || MODEL_NAME.Contains("SE7116"))
                        {
                            SendCmd("df -h");
                            Hold(2000);
                            line = txt_Rx.Text.Split('\r');
                            for (i = line.GetUpperBound(0); i >= 0; i--)
                            {
                                if (line[i].Contains("/dev/sda1"))
                                {
                                    SendCmd("mount /dev/sdb1 /mnt/usb");
                                    Hold(300);
                                    break;
                                }
                                if (line[i].Contains("df -h"))  // 表示沒有搜尋到"/dev/sda1"
                                {
                                    SendCmd("mount /dev/sda1 /mnt/usb");
                                    Hold(300);
                                    break;  // for
                                }
                            }
                        }
                        SendCmd("cd /jffs2/");
                        Hold(100);
                        //SendCmd("chmod 755 *");
                        //Hold(100);
                        //SendCmd("sync");
                        //Hold(100);
                        // 檢測USB有無mount
                        SendCmd("df -h");
                        Hold(2000);
                        line = txt_Rx.Text.Split('\r');
                        if (MODEL_NAME.Contains("SE7816"))
                        {
                            for (i = line.GetUpperBound(0); i >= 0; i--)
                            {
                                if (line[i].Contains("/mnt/usb"))
                                {
                                    SendCmd("cp " + USB_function + " /mnt/usb/");
                                    Hold(500); // 讓檔案有足夠的時間能確實的被寫入到裝置
                                    SendCmd("/mnt/usb/" + USB_function);
                                    Hold(300);
                                    break;
                                }
                                if (line[i].Contains("df -h"))
                                {
                                    break;  // for
                                }
                            }
                        }
                        else if (MODEL_NAME.Contains("SE7416"))
                        {
                            for (i = line.GetUpperBound(0); i >= 0; i--)
                            {
                                if (line[i].Contains("/mnt/usb"))
                                {
                                    SendCmd("cp " + USB_function + " /mnt/vfat/");
                                    Hold(500); // 讓檔案有足夠的時間能確實的被寫入到裝置
                                    SendCmd("/mnt/vfat/" + USB_function);
                                    Hold(300);
                                    break;
                                }
                                if (line[i].Contains("df -h"))
                                {
                                    break;  // for
                                }
                            }
                        }
                        else if (MODEL_NAME.Contains("SE7116"))
                        {
                            for (i = line.GetUpperBound(0); i >= 0; i--)
                            {
                                if (line[i].Contains("/mnt/usb"))
                                {
                                    SendCmd("cp " + USB_function + " /mnt/usb/");
                                    Hold(500); // 讓檔案有足夠的時間能確實的被寫入到裝置
                                    SendCmd("/mnt/usb/" + USB_function);
                                    Hold(300);
                                    break;
                                }
                                if (line[i].Contains("df -h"))
                                {
                                    break;  // for
                                }
                            }
                        }

                        line = txt_Rx.Text.Split('\r');
                        RunTest_result = 2;
                        for (i = line.GetUpperBound(0); i >= 0; i--)
                        {
                            //if (line[i].Contains("GET USB OK"))
                            if (line[i].ToUpper().Contains("USB") & line[i].ToUpper().Contains("OK"))
                            {
                                RunTest_result = 1;
                                break;  // for
                            }
                            if (line[i].Contains("df -h"))
                            {
                                break;  // fail，所以搜尋到df -h就停止搜尋
                            }
                        }
                        if (RunTest_result == 1)
                        {
                            lblStatus.Text = cmd[0].ToUpper() + " Test Pass !";
                        }
                        else
                        {
                            RecNote(idx, cmd[0].ToUpper() + " Test Fail.");
                            lblStatus.Text = cmd[0].ToUpper() + " Test Fail !";
                        }
                        break;
                    case "WATCHDOG":
                        SendCmd("atop_hwd &");
                        RunTest_result = 1;
                        WaitKey = "Disable Hardware Watchdog";
                        if (Hold(30000) == false)
                        {
                            RunTest_result = 2;
                        }
                        break;
                    case "POWER":

                        break;
                    case "FTP":

                        break;
                    case "GWD":

                        break;
                    case "CPU":

                        break;
                    case "MEMORY":

                        break;
                    case "NETWORK":

                        break;
                    case "SLEEP":
                        duration = Math.Round(TimeUnit(idx, 1) / 60, 2);
                        pause(duration);
                        break;
                    case "SYSTEM":
                        duration = Math.Round(TimeUnit(idx, 4) / 60, 2);
                        break;
                    case "RESTORE":
                        SendCmd("cd /jffs2/");
                        Hold(100);
                        //SendCmd("chmod 755 *");
                        //Hold(100);
                        SendCmd("killall atop_restored");
                        Hold(100);
                        SendCmd("killall restored");
                        Hold(100);
                        MessageBox.Show("按下確定鍵後，請在 10 秒內按壓 default鍵 2 秒以上，直到聽見 bee 一聲後放開 default鍵。  ", cmd[0] + " test", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        SendCmd("./restored &");
                        RunTest_result = 1;
                        WaitKey = "OK";
                        if (Hold(10000) == false)
                        {
                            RecNote(idx, cmd[0].ToUpper() + " Test Fail.");
                            RunTest_result = 2;
                        }
                        SendCmd("killall restored");
                        break;
                    default:
                        break;
                }
                // Excel log
                if (idx_funlog != -1)
                {
                    if (RunTest_result == 1)
                    {
                        TEST_RESULT[idx_funlog] = TEST_RESULT[idx_funlog] + "o";
                    }
                    else if (RunTest_result == 2)
                    {
                        TEST_RESULT[idx_funlog] = TEST_RESULT[idx_funlog] + "X";
                    }
                    else if (RunTest_result == 3)
                    {
                        TEST_RESULT[idx_funlog] = TEST_RESULT[idx_funlog] + "-";
                    }
                }
                return RunTest_result;  // switch use
            }
            catch (Exception ex)
            {
                RecNote(idx, ex.Message);
                SendCmd(((char)3).ToString()); // ((char)3):Ctrl+c
                MessageBox.Show(ex.Message + "\n\n" + ex.StackTrace, "Run test error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                //Run_Stop = true; // 發生錯誤，自動測試就停止
                return RunTest_result = 3;
            }
            finally
            {
                if (RunTest_result == 1)
                {
                    lblFunction[idx].BackColor = Color.FromArgb(0, 255, 0); /* 1:PASS Green */
                }
                else if (RunTest_result == 2)
                {
                    lblFunction[idx].BackColor = Color.FromArgb(255, 0, 0); /* 2:Fail Red */
                }
                else if (RunTest_result == 3) { lblFunction[idx].BackColor = Color.FromArgb(255, 255, 0); /* 3:error Yellow */}
                else if (RunTest_result == 0) { lblFunction[idx].BackColor = Color.FromArgb(255, 255, 255); /* 0 */}
            }
        }

        /// <summary>
        /// 回傳關鍵字所在的整行文字
        /// </summary>
        /// <param name="Key">目標關鍵字</param>
        /// <param name="stopSearch">如果目標關鍵字不存在，則搜尋到stopSearch時停止搜尋</param>
        /// <returns>回傳整行文字，關鍵字不存在則回傳string.Empty</returns>
        private string GetLine(string Key, string stopSearch)
        {
            int i;
            string[] line;
            string get_line = string.Empty;
            line = txt_Rx.Text.Split('\r');
            for (i = line.GetUpperBound(0); i >= 0; i--)
            {
                if (line[i].Contains(Key))
                {
                    get_line = line[i].Replace("\n", "");
                    break;  // for
                }
                else if (line[i].Contains(stopSearch))
                {
                    break;
                }
            }
            return get_line;
        }

        private void txt_Tx_KeyDown(object sender, KeyEventArgs e)
        {
            // Initialize the flag to false.
            nonNumberEntered = false;
            int key = e.KeyValue;
            //if (e.Control != true)//如果沒按Ctrl鍵
            //    return;

            switch (key)
            {
                case 13:
                    //按下Enter以後
                    SendCmd(txt_Tx.Text);
                    txt_Tx.Text = string.Empty;
                    nonNumberEntered = true;
                    break;
                case 38:
                    //按下向上鍵以後
                    SendCmd(((char)27).ToString() + ((char)91).ToString() + ((char)65).ToString()); // ←[A
                    nonNumberEntered = true;
                    break;
                case 40:
                    //按下向下鍵以後
                    SendCmd(((char)27).ToString() + ((char)91).ToString() + ((char)66).ToString()); // ←[B
                    nonNumberEntered = true;
                    break;
                default:
                    break;
            }
        }

        private void txt_Tx_KeyPress(object sender, KeyPressEventArgs e)
        {
            // KeyChar 無法抓取上下左右鍵
            // http://msdn.microsoft.com/zh-tw/library/system.windows.forms.keyeventargs.handled%28v=vs.110%29.aspx
            // Check for the flag being set in the KeyDown event.
            if (nonNumberEntered)
            {
                // Stop the character from being entered into the control since it is non-numerical.
                e.Handled = true;
            }
        }

        private void consoleToolStripMenuItem_CheckStateChanged(object sender, EventArgs e)
        {
            if (consoleToolStripMenuItem.Checked)
                tabControl1.SelectedTab = tabPage1;
            else if (tabControl1.SelectedTab == tabPage3)
                tabControl1.SelectedTab = tabPage3;
            else
                tabControl1.SelectedTab = tabPage2;
        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabControl1.SelectedIndex == 0)
                consoleToolStripMenuItem.Checked = true;
            else
                consoleToolStripMenuItem.Checked = false;
        }

        #region 自動保持 TextBox 垂直捲軸在最下方

        private void txt_Rx_TextChanged(object sender, EventArgs e)
        {
            // 自動保持捲軸在最下方
            txt_Rx.SelectionStart = txt_Rx.Text.Length;
            txt_Rx.ScrollToCaret();
        }

        private void txt_Note_TextChanged(object sender, EventArgs e)
        {
            txt_Note.SelectionStart = txt_Note.Text.Length;
            txt_Note.ScrollToCaret();
        }

        private void txt_Rx_EUT_TextChanged(object sender, EventArgs e)
        {
            // 自動保持捲軸在最下方
            txt_Rx_EUT.SelectionStart = txt_Rx_EUT.Text.Length;
            txt_Rx_EUT.ScrollToCaret();
        }

        #endregion 自動保持 TextBox 垂直捲軸在最下方

        private void composingTmr_Tick(object sender, EventArgs e)
        {
            int idx, X_StartPos, Y_StartPos;
            int X, Y;   // every position(location) of the panel
            X_StartPos = 52; Y_StartPos = 25;    // initial position(location) of the panel
            row_num = (this.Height - Y_StartPos) / (lblFunction[0].Height * 2) - 6;
            for (idx = 0; idx < TestFun_MaxIdx; idx++)    // composing Label
            {
                X = X_StartPos + (idx / row_num) * X_StartPos * 3;
                Y = Y_StartPos + (lblFunction[idx].Height * (idx % row_num) * 2);
                lblFunction[idx].Location = new Point(X, Y);
                lblFunction[idx].Visible = true;
            }
            composingTmr.Enabled = false;
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            if (lblFunction[0] != null)
            {
                composingTmr.Enabled = true;
            }
        }

        private void 從這個測項開始測試ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Test_Idx = MOUSE_Idx;
            chooseStart = true;
            cmdStart_Click(null, null);
        }

        private void 無限次測試這個測項ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Run_Stop = false;
            do
            {
                TEST_STATUS[MOUSE_Idx] = RunTest(MOUSE_Idx);
                if (STOP_WHEN_FAIL && Convert.ToInt32(TEST_STATUS[MOUSE_Idx]) == 2)
                {
                    return;
                }
                Hold(1000);
            } while (Run_Stop == false);
        }

        private void 用Putty開啟ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string[] cmd;
            cmd = Convert.ToString(lblFunction[MOUSE_Idx].Tag).Split(' ');
            // Uses the ProcessStartInfo class to start new processes
            ProcessStartInfo startInfo = new ProcessStartInfo("putty.exe");
            startInfo.UseShellExecute = false;
            string info;
            if (cmd[0].ToUpper() == "CONSOLE-DUT" || cmd[0].ToUpper() == "CONSOLE-EUT")
            {
                if (cmd[0].ToUpper() == "CONSOLE-DUT")
                {
                    serialPort1_Close();
                    //if (serialPort1.IsOpen) { serialPort1.Close(); }
                    info = "-serial " + cmbDutCom.Text + " -sercfg 115200,8,n,1,n";
                    startInfo.Arguments = info;
                }
                else if (cmd[0].ToUpper() == "CONSOLE-EUT")
                {
                    serialPort2_Close();
                    //if (serialPort2.IsOpen) { serialPort2.Close(); }
                    info = "-serial " + cmbEutCom.Text + " -sercfg 115200,8,n,1,n";
                    startInfo.Arguments = info;
                }
                Process.Start(startInfo);
            }
            else if (cmd[0].ToUpper() == "TELNET")
            {
                //USR = cmd[1];
                //if (cmd.GetUpperBound(0) > 1) { PWD = cmd[2]; }
                //else { PWD = string.Empty; }
                startInfo.Arguments = "-telnet -t " + TARGET_IP;
                Process.Start(startInfo);
                Hold(1000);
                SendKeys.SendWait(USR + "{ENTER}");
                Hold(1000);
                SendKeys.SendWait(PWD + "{ENTER}");
            }
            else if (cmd[0].ToUpper() == "POWER")
            {
            }
            lblFunction[MOUSE_Idx].BackColor = Color.FromArgb(255, 255, 255);
        }

        #region Hold / atop_timer

        public bool Hold(long timeout)
        {
            bool tmp_Hold = true;
            long delay = 0;
            WAIT = true;
            if (timeout > 0) { delay = timeout / 10; }
            while (WAIT)
            {
                Application.DoEvents();
                Thread.Sleep(10);
                if (timeout > 0)
                {
                    if (delay > 0)
                    {
                        delay -= 1;
                    }
                    else
                    {
                        tmp_Hold = false;   // 時間等到底
                        break;
                    }
                }
            }
            return tmp_Hold;
        }

        #endregion Hold / atop_timer

        #region lblStatus.ForeColor 隨著測試項目改變而變化Color

        // RGB to Hex
        // http://www.rapidtables.com/convert/color/rgb-to-hex.htm
        private void timer2_Tick(object sender, EventArgs e)
        {
            //Debug.Print(lblStatus.ForeColor.ToArgb().ToString());
            if (lblStatus.ForeColor.ToArgb() > 10 * 65536)
            {
                int hex_tmp = Convert.ToInt32(lblStatus.ForeColor.ToArgb());
                lblStatus.ForeColor = Color.FromArgb(hex_tmp - 50 * 65536);
            }
        }

        private void lblStatus_TextChanged(object sender, EventArgs e)
        {
            lblStatus.ForeColor = Color.FromArgb(255 * 65536);
        }

        #endregion lblStatus.ForeColor 隨著測試項目改變而變化Color

        public int ReCntTelnet(long timeout)
        {
            if (serialPort1.IsOpen)
            {
                WaitKey = "login";
                enterTmr.Enabled = true;    // 5秒按一次enter
                if (Hold(timeout * 1000) == false)
                {
                    enterTmr.Enabled = false;
                    return 2;
                }
                else
                {
                    enterTmr.Enabled = false;
                    return 1;
                }
            }
            else
            {
                int tm = 0;
                lblStatus.Text = "等待系統重開機...";
                do
                {
                    Hold(1000);
                    tm += 1;
                    if (tm > (timeout / 2))
                    {
                        lblStatus.Text = "連線失敗";
                        return 2; // 逾時
                    }
                } while (objping.Send(TARGET_IP, 1000).Status != IPStatus.Success);

                telnet = new TcpClient();
                if (!telnet.Connected)
                {
                    try
                    {
                        telnet.Connect(TARGET_IP, 23);   // 連接23端口 (Telnet的默認端口)
                        telentStream = telnet.GetStream();  // 建立網路資料流，將字串寫入串流

                        if (telnet.Connected)
                        {
                            //lblStatus.Text = "連線成功，正在登錄...";
                            lblStatus.Text = "正在登錄...";
                            Hold(1000);
                            // 背景telnet接收執行續
                            ThreadStart backgroundReceive = new ThreadStart(telnet_Receive);
                            Thread rcvThread = new Thread(backgroundReceive);
                            rcvThread.IsBackground = true;
                            rcvThread.Start();

                            bytWrite_telnet = System.Text.Encoding.Default.GetBytes(USR + ((char)13).ToString());
                            telentStream.Write(bytWrite_telnet, 0, bytWrite_telnet.Length);
                            Hold(200);
                            bytWrite_telnet = System.Text.Encoding.Default.GetBytes(PWD + ((char)13).ToString());
                            telentStream.Write(bytWrite_telnet, 0, bytWrite_telnet.Length);
                            lblStatus.Text = "連線成功";
                            return 1;
                        }
                    }
                    catch (Exception)
                    {
                        return 2;   // 目標主機連線沒反應
                    }
                }
            }
            return 2;
        }

        #region 驗證IP

        /// <summary>
        /// 驗證IP
        /// </summary>
        /// <param name="source"></param>
        /// <returns>規則運算式尋找到符合項目，則為 true，否則為 false</returns>
        public static bool IsIP(string source)
        {
            // Regex.IsMatch 方法 (String, String, RegexOptions)
            // 表示指定的規則運算式是否使用指定的比對選項，在指定的輸入字串中尋找相符項目
            return Regex.IsMatch(source, @"^(25[0-5]|2[0-4][0-9]|[0-1]{1}[0-9]{2}|[1-9]{1}[0-9]{1}|[1-9])\.(25[0-5]|2[0-4][0-9]|[0-1]{1}[0-9]{2}|[1-9]{1}[0-9]{1}|[1-9]|0)\.(25[0-5]|2[0-4][0-9]|[0-1]{1}[0-9]{2}|[1-9]{1}[0-9]{1}|[1-9]|0)\.(25[0-5]|2[0-4][0-9]|[0-1]{1}[0-9]{2}|[1-9]{1}[0-9]{1}|[0-9])$", RegexOptions.IgnoreCase);
        }

        public static bool HasIP(string source)
        {
            return Regex.IsMatch(source, @"(25[0-5]|2[0-4][0-9]|[0-1]{1}[0-9]{2}|[1-9]{1}[0-9]{1}|[1-9])\.(25[0-5]|2[0-4][0-9]|[0-1]{1}[0-9]{2}|[1-9]{1}[0-9]{1}|[1-9]|0)\.(25[0-5]|2[0-4][0-9]|[0-1]{1}[0-9]{2}|[1-9]{1}[0-9]{1}|[1-9]|0)\.(25[0-5]|2[0-4][0-9]|[0-1]{1}[0-9]{2}|[1-9]{1}[0-9]{1}|[0-9])", RegexOptions.IgnoreCase);
        }

        #endregion 驗證IP

        #region FTP

        /// <summary>
        /// FTP 上傳檔案至目標位置
        /// </summary>
        /// <param name="FTPAddress">目標位置</param>
        /// <param name="filePath">上傳的檔案</param>
        /// <param name="username">帳號</param>
        /// <param name="password">密碼</param>
        public void uploadFile(string IP, string filePath, string username, string password)
        {
            //if (!IP.StartsWith("ftp://")) { IP = "ftp://" + IP; }
            string FTPAddress = "ftp://" + IP + "//jffs2";
            // Create FTP request
            FtpWebRequest request = (FtpWebRequest)FtpWebRequest.Create(FTPAddress + "/" + Path.GetFileName(filePath));
            // This example assumes the FTP site uses anonymous logon.
            request.Credentials = new NetworkCredential(username, password);
            request.UsePassive = true;
            request.UseBinary = true;
            request.KeepAlive = false;
            request.ReadWriteTimeout = 7000;
            request.Timeout = 3000;
            request.Method = WebRequestMethods.Ftp.UploadFile;

            // Load the file
            FileStream stream = File.OpenRead(filePath);
            byte[] buffer = new byte[stream.Length];

            stream.Read(buffer, 0, buffer.Length);
            stream.Close();

            // Upload file
            Stream reqStream = request.GetRequestStream();
            reqStream.Write(buffer, 0, buffer.Length);
            reqStream.Close();

            //Debug.Print("Uploaded Successfully !");
        }

        /// <summary>
        /// 列出 FTP 目錄的內容，並檢查檔案是否存在內容中。
        /// </summary>
        /// <param name="IP">目標 IP</param>
        /// <param name="fileName">欲檢查的檔案</param>
        /// <param name="username">帳號</param>
        /// <param name="password">密碼</param>
        /// <returns>true代表存在；false代表不存在</returns>
        public bool checkFile(string IP, string fileName, string username, string password)
        {
            string FTPAddress = "ftp://" + IP + "//jffs2";
            //Create FTP request
            FtpWebRequest request = (FtpWebRequest)FtpWebRequest.Create(FTPAddress);
            // This example assumes the FTP site uses anonymous logon.
            request.Credentials = new NetworkCredential(username, password);
            request.UsePassive = true;
            request.UseBinary = true;
            request.KeepAlive = false;
            request.ReadWriteTimeout = 5000;
            request.Timeout = 3000;
            request.Method = WebRequestMethods.Ftp.ListDirectory;

            string responseTmp = string.Empty;
            FtpWebResponse response = (FtpWebResponse)request.GetResponse();
            Stream responseStream = response.GetResponseStream();
            StreamReader reader = new StreamReader(responseStream);
            responseTmp = reader.ReadToEnd();
            if (responseTmp.Contains(fileName))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        ///* Rename File */
        //public void renameFile(string IP, string currentFileNameAndPath, string newFileName, string username, string password)
        //{
        //    string FTPAddress = "ftp://" + IP + "//jffs2";
        //    //Create FTP request
        //    FtpWebRequest request = (FtpWebRequest)FtpWebRequest.Create(FTPAddress);
        //    // This example assumes the FTP site uses anonymous logon.
        //    request.Credentials = new NetworkCredential(username, password);
        //    /* When in doubt, use these options */
        //    request.UseBinary = true;
        //    request.UsePassive = true;
        //    request.KeepAlive = false;
        //    /* Specify the Type of FTP Request */
        //    request.Method = WebRequestMethods.Ftp.Rename;
        //    /* Rename the File */
        //    request.RenameTo = newFileName;
        //    /* Establish Return Communication with the FTP Server */
        //    request = (FtpWebResponse)request.GetResponse();
        //    /* Resource Cleanup */
        //    request.Close();
        //    request = null;
        //}

        ///* Delete File */
        //public void deleteFile(string deleteFile)
        //{
        //    try
        //    {
        //        /* Create an FTP Request */
        //        ftpRequest = (FtpWebRequest)WebRequest.Create(host + "/" + deleteFile);
        //        /* Log in to the FTP Server with the User Name and Password Provided */
        //        ftpRequest.Credentials = new NetworkCredential(user, pass);
        //        /* When in doubt, use these options */
        //        ftpRequest.UseBinary = true;
        //        ftpRequest.UsePassive = true;
        //        ftpRequest.KeepAlive = true;
        //        /* Specify the Type of FTP Request */
        //        ftpRequest.Method = WebRequestMethods.Ftp.DeleteFile;
        //        /* Establish Return Communication with the FTP Server */
        //        ftpResponse = (FtpWebResponse)ftpRequest.GetResponse();
        //        /* Resource Cleanup */
        //        ftpResponse.Close();
        //        ftpRequest = null;
        //    }
        //    catch (Exception ex) { Console.WriteLine(ex.ToString()); }
        //    return;
        //}

        #endregion FTP

        private void txt_mac1_TextChanged(object sender, EventArgs e)
        {
            if (txt_mac1.Text.Length < 6)
            {
                start_Command1.Enabled = false;
                start_Command2.Enabled = false;
            }
            if (txt_mac1.Text == "ffffff" || txt_mac1.Text == "FFFFFF")
            {
                txt_mac1.Text = string.Empty;
            }
        }

        private void txt_mac1_KeyPress(object sender, KeyPressEventArgs e)
        {
            try
            {
                if (cmdOpeFile.Text == "檔案名稱")
                {
                    MessageBox.Show("請先選擇檔案名稱 (測試產品)", "txt_mac1_KeyPress Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    e.Handled = true; //按下的資料不會輸入
                }

                if (!(97 <= (int)e.KeyChar && (int)e.KeyChar <= 102) &&  // != a~f
                    !(48 <= (int)e.KeyChar && (int)e.KeyChar <= 57) &&   // != 0~9
                    !(65 <= (int)e.KeyChar && (int)e.KeyChar <= 70) &&   // != A~F
                    (int)e.KeyChar != 8) //Backspace
                {
                    e.Handled = true;
                }
                else
                {
                    if (txt_mac1.Text.Length == 5)
                    {
                        if ((int)e.KeyChar == 49 || (int)e.KeyChar == 51 || (int)e.KeyChar == 53 || (int)e.KeyChar == 55 || (int)e.KeyChar == 57 ||
                            (int)e.KeyChar == 66 || (int)e.KeyChar == 68 || (int)e.KeyChar == 70 ||
                            (int)e.KeyChar == 98 || (int)e.KeyChar == 100 || (int)e.KeyChar == 102)
                        {
                            // 奇數
                            start_Command1.Enabled = false;
                            start_Command2.Enabled = false;
                        }
                        else
                        {
                            // 偶數
                            start_Command1.Enabled = true;
                            start_Command2.Enabled = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "txt_mac1_KeyPress Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                start_Command1.Enabled = false;
                start_Command2.Enabled = false;
            }
            finally { }
        }

        private void start_Command1_Click(object sender, EventArgs e)
        {
            string set_result = string.Empty;
            ushort n = System.Convert.ToUInt16(((Button)sender).Tag);

            try
            {
                RunTest(0); // Console-DUT

                #region 重啟進入 manufactory mode

                start_Command1.Enabled = false;
                start_Command2.Enabled = false;
                txt_mac1.Enabled = false;
                if (n == 0)
                {
                    if (MODEL_NAME.Contains("SE7816") || MODEL_NAME.Contains("SE7116") || MODEL_NAME.Contains("SE4116"))
                    {
                        serialPort1.Write("atop_restart" + ((char)13).ToString());
                        Hold(100);
                    }
                    else if (MODEL_NAME.Contains("SE7416"))
                    {
                        serialPort1.Write("restart" + ((char)13).ToString());
                        Hold(100);
                    }

                    do
                    {
                        //SendCmd(((char)27).ToString());
                        serialPort1.Write(((char)27).ToString());   //Esc
                        WaitKey = ":TFTP";
                        Hold(1000);
                    } while (WAIT);

                    serialPort1.Write(((char)21).ToString());    //Ctrl + u
                    Hold(100);

                    if (MODEL_NAME.Contains("SE7816"))
                    {
                        serialPort1.Write("atop8377" + ((char)13).ToString());
                    }
                    else if (MODEL_NAME.Contains("SE7416"))
                    {
                        serialPort1.Write("atop438" + ((char)13).ToString());
                    }
                    else if (MODEL_NAME.Contains("SE7116") || MODEL_NAME.Contains("SE4116"))
                    {
                        serialPort1.Write("atop1022" + ((char)13).ToString());
                    }
                }

                #endregion 重啟進入 manufactory mode

                serialPort1.Write(((char)13).ToString());
                WaitKey = "Manufactory";
                if (Hold(1000))
                {
                    if (MODEL_NAME.Contains("SE7816"))
                    {
                        set_result = Set_SE7816();
                    }
                    else if (MODEL_NAME.Contains("SE7416"))
                    {
                        set_result = Set_SE7416();
                    }
                    else if (MODEL_NAME.Contains("SE7116"))
                    {
                        set_result = Set_SE7116();
                    }
                    else if (MODEL_NAME.Contains("SE4116"))
                    {
                        set_result = Set_SE4116();
                    }
                }

                Shell(appPATH, "arp-d.bat");

                if (set_result.Contains("error"))
                {
                    MessageBox.Show("出廠設定寫入失敗 !", "Orz", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                }
                else if (set_result.Contains("successful"))
                {
                    if (MessageBox.Show("出廠設定成功 ! \n\n 要直接進行自動測試嗎 ?     ", "^_^", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    {
                        do
                        {
                            lblStatus.Text = "等待開機完成 O_o";
                            Hold(700);
                            lblStatus.Text = "等待開機完成 o_O";
                            Hold(200);
                        } while (objping.Send(TARGET_IP, 500).Status != IPStatus.Success);
                        lblStatus.Text = "";
                        serialPort1_Close();
                        Hold(10000);
                        cmdStart_Click(null, null);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\n\n" + ex.StackTrace, "start_Command Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                start_Command1.Enabled = true;
                start_Command2.Enabled = true;
                txt_mac1.Enabled = true;
            }
        }

        #region Bootloader setting

        private string Set_SE7816()
        {
            //string strWait = string.Empty;

            #region MAC , 16進制與10進制轉換

            int i;
            Int32 mac_int;
            string mac_str;
            string MAC1, MAC2, MAC3, MAC4, MAC5, MAC6;
            string 字首零的判斷 = txt_mac1.Text;
            // 進制轉換 http://aabbc1122.blog.163.com/blog/static/57043257201211331433715/
            Console.WriteLine(txt_mac1.Text.Substring(0, 6));
            mac_int = Convert.ToInt32(txt_mac1.Text.Substring(0, 6), 16);   // 十六進制轉十進制,Convert.ToInt32("CC", 16));
            // mac1
            mac_str = Convert.ToString(mac_int, 16);    // 十進制轉十六進制,Convert.ToString(166, 16));
            if (字首零的判斷.Substring(0, 1) == "0") { mac_str = "0" + mac_str; }
            txt_mac1.Text = mac_str.ToUpper();
            MAC1 = String.Format("00:60:e9:{0}:{1}:{2}", txt_mac1.Text.Substring(0, 2), txt_mac1.Text.Substring(2, 2), txt_mac1.Text.Substring(4, 2));
            Mac_forExcel = MAC1;
            // mac2
            mac_str = Convert.ToString((mac_int + 1), 16);    // 十進制轉十六進制,Convert.ToString(166, 16));
            if (字首零的判斷.Substring(0, 1) == "0") { mac_str = "0" + mac_str; }
            txt_mac2.Text = mac_str.ToUpper();
            MAC2 = String.Format("00:60:e9:{0}:{1}:{2}", txt_mac2.Text.Substring(0, 2), txt_mac2.Text.Substring(2, 2), txt_mac2.Text.Substring(4, 2));
            // mac3
            mac_str = Convert.ToString((mac_int + 2), 16);    // 十進制轉十六進制,Convert.ToString(166, 16));
            if (字首零的判斷.Substring(0, 1) == "0") { mac_str = "0" + mac_str; }
            txt_mac3.Text = mac_str.ToUpper();
            MAC3 = String.Format("00:60:e9:{0}:{1}:{2}", txt_mac3.Text.Substring(0, 2), txt_mac3.Text.Substring(2, 2), txt_mac3.Text.Substring(4, 2));
            // mac4
            mac_str = Convert.ToString((mac_int + 3), 16);    // 十進制轉十六進制,Convert.ToString(166, 16));
            if (字首零的判斷.Substring(0, 1) == "0") { mac_str = "0" + mac_str; }
            txt_mac4.Text = mac_str.ToUpper();
            MAC4 = String.Format("00:60:e9:{0}:{1}:{2}", txt_mac4.Text.Substring(0, 2), txt_mac4.Text.Substring(2, 2), txt_mac4.Text.Substring(4, 2));
            // mac5
            mac_str = Convert.ToString((mac_int + 4), 16);    // 十進制轉十六進制,Convert.ToString(166, 16));
            if (字首零的判斷.Substring(0, 1) == "0") { mac_str = "0" + mac_str; }
            txt_mac5.Text = mac_str.ToUpper();
            MAC5 = String.Format("00:60:e9:{0}:{1}:{2}", txt_mac5.Text.Substring(0, 2), txt_mac5.Text.Substring(2, 2), txt_mac5.Text.Substring(4, 2));
            // mac6
            mac_str = Convert.ToString((mac_int + 5), 16);    // 十進制轉十六進制,Convert.ToString(166, 16));
            if (字首零的判斷.Substring(0, 1) == "0") { mac_str = "0" + mac_str; }
            txt_mac6.Text = mac_str.ToUpper();
            MAC6 = String.Format("00:60:e9:{0}:{1}:{2}", txt_mac6.Text.Substring(0, 2), txt_mac6.Text.Substring(2, 2), txt_mac6.Text.Substring(4, 2));

            #endregion MAC , 16進制與10進制轉換

            ///////////////////////Set MAC address
            serialPort1.Write("1");
            Hold(200);
            // MAC1 address
            serialPort1.Write("1");
            Hold(200);
            serialPort1.Write(MAC1 + ((char)13).ToString());
            WaitKey = "OK";
            if (!Hold(3000)) { return "error"; };
            // MAC2 address
            serialPort1.Write("2");
            Hold(100);
            serialPort1.Write(MAC2 + ((char)13).ToString());
            WaitKey = "OK";
            if (!Hold(3000)) { return "error"; };
            // MAC3 address
            serialPort1.Write("3");
            Hold(200);
            serialPort1.Write(MAC3 + ((char)13).ToString());
            WaitKey = "OK";
            if (!Hold(3000)) { return "error"; };
            // MAC4 address
            serialPort1.Write("4");
            Hold(200);
            serialPort1.Write(MAC4 + ((char)13).ToString());
            WaitKey = "OK";
            if (!Hold(3000)) { return "error"; };
            // MAC5 address
            serialPort1.Write("5");
            Hold(200);
            serialPort1.Write(MAC5 + ((char)13).ToString());
            WaitKey = "OK";
            if (!Hold(3000)) { return "error"; };
            // MAC6 address
            serialPort1.Write("6");
            Hold(200);
            serialPort1.Write(MAC6 + ((char)13).ToString());
            WaitKey = "OK";
            if (!Hold(3000)) { return "error"; };
            // exit MAC setting
            serialPort1.Write("0");
            Hold(200);

            ///////////////////////Set LAN address
            serialPort1.Write("2");
            Hold(200);
            if (MODEL_NAME.Contains("512M"))
            {
                for (i = 1; i <= 6; i++)
                {
                    // LANi
                    serialPort1.Write(Convert.ToString(i));
                    Hold(200);
                    // IP
                    serialPort1.Write("1");
                    Hold(200);
                    serialPort1.Write("100.100.10" + Convert.ToString(i - 1) + ".17" + ((char)13).ToString());
                    Hold(200);
                    //WaitKey = "OK";
                    //if (!Hold(3000)) { return "error"; };
                    // Netmask
                    serialPort1.Write("2");
                    Hold(200);
                    serialPort1.Write("255.255.255.0" + ((char)13).ToString());
                    Hold(200);
                    //WaitKey = "OK";
                    //if (!Hold(3000)) { return "error"; };
                    // Gateway
                    serialPort1.Write("3");
                    Hold(200);
                    serialPort1.Write("100.100.10" + Convert.ToString(i - 1) + ".254" + ((char)13).ToString());
                    Hold(200);
                    //WaitKey = "OK";
                    //if (!Hold(3000)) { return "error"; };
                    // Mode
                    serialPort1.Write("4");
                    Hold(200);
                    serialPort1.Write("0" + ((char)13).ToString());
                    Hold(200);
                    //WaitKey = "OK";
                    //if (!Hold(3000)) { return "error"; };
                    // Speed
                    serialPort1.Write("5");
                    Hold(200);
                    serialPort1.Write("0" + ((char)13).ToString());
                    Hold(200);
                    //WaitKey = "OK";
                    //if (!Hold(3000)) { return "error"; };
                    // Fiber Mode
                    if (MODEL_NAME.Contains("512M-F100") && (i == 1 || i == 2))
                    {
                        serialPort1.Write("6");
                        Hold(200);
                        serialPort1.Write("1" + ((char)13).ToString());
                        Hold(200);
                        //WaitKey = "OK";
                        //if (!Hold(3000)) { return "error"; };
                    }
                    // exit LANi setting
                    serialPort1.Write("0");
                    WaitKey = "Ok";
                    if (!Hold(5000)) { return "error"; };
                }
            }
            else if (MODEL_NAME.Contains("256M"))
            {
                for (i = 1; i <= 6; i++)
                {
                    // LANi
                    serialPort1.Write(Convert.ToString(i));
                    Hold(100);
                    // IP
                    serialPort1.Write("1");
                    Hold(100);
                    serialPort1.Write("192.168." + Convert.ToString(i - 1) + ".1" + ((char)13).ToString());
                    WaitKey = "OK";
                    if (!Hold(3000)) { return "error"; };
                    // Netmask
                    serialPort1.Write("2");
                    Hold(100);
                    serialPort1.Write("255.255.255.0" + ((char)13).ToString());
                    WaitKey = "OK";
                    if (!Hold(3000)) { return "error"; };
                    // Gateway
                    serialPort1.Write("3");
                    Hold(100);
                    serialPort1.Write("192.168." + Convert.ToString(i - 1) + ".254" + ((char)13).ToString());
                    WaitKey = "OK";
                    if (!Hold(3000)) { return "error"; };
                    // Mode
                    serialPort1.Write("4");
                    Hold(100);
                    serialPort1.Write("0" + ((char)13).ToString());
                    WaitKey = "OK";
                    if (!Hold(3000)) { return "error"; };
                    // Speed
                    serialPort1.Write("5");
                    Hold(100);
                    serialPort1.Write("0" + ((char)13).ToString());
                    WaitKey = "OK";
                    if (!Hold(3000)) { return "error"; };
                    // exit LANi setting
                    serialPort1.Write("0");
                    WaitKey = "Ok";
                    if (!Hold(3000)) { return "error"; };
                }
            }
            // exit LAN setting
            serialPort1.Write("0");
            Hold(200);

            ///////////////////////Setup Routing Netmask default
            serialPort1.Write("3");
            Hold(200);
            for (i = 1; i <= 6; i++)
            {
                // Routing Netmask i
                serialPort1.Write(Convert.ToString(i));
                Hold(200);
                serialPort1.Write("0.0.0.0" + ((char)13).ToString());
                Hold(200);
                //WaitKey = "OK";
                //if (!Hold(3000)) { return "error"; };
            }
            // exit Routing Netmask setting
            serialPort1.Write("0");
            //Hold(200);
            WaitKey = ((char)10).ToString() + ":";
            if (!Hold(3000)) { return "error"; };

            ///////////////////////Setup DNS default
            serialPort1.Write("4");
            Hold(200);
            for (i = 1; i <= 6; i++)
            {
                // DNS i
                serialPort1.Write(Convert.ToString(i));
                Hold(200);
                serialPort1.Write("0.0.0.0" + ((char)13).ToString());
                Hold(200);
                //WaitKey = "OK";
                //if (!Hold(3000)) { return "error"; };
            }
            // exit DNS setting
            serialPort1.Write("0");
            //Hold(200);
            WaitKey = ((char)10).ToString() + ":";
            if (!Hold(3000)) { return "error"; };

            ///////////////////////Setup Download port
            serialPort1.Write("5");
            Hold(200);
            serialPort1.Write("65535" + ((char)13).ToString());
            WaitKey = "OK";
            if (!Hold(3000)) { return "error"; };

            ///////////////////////Setup Magic code
            serialPort1.Write("6");
            Hold(200);
            serialPort1.Write("ATOP" + ((char)13).ToString());
            WaitKey = "OK";
            if (!Hold(3000)) { return "error"; };

            ///////////////////////Setup Model name
            serialPort1.Write("7");
            Hold(200);
            serialPort1.Write(MODEL_NAME + ((char)13).ToString());
            WaitKey = "OK";
            if (!Hold(3000)) { return "error"; };

            ///////////////////////Setup Device name
            serialPort1.Write("8");
            Hold(200);
            serialPort1.Write("0060e9" + txt_mac1.Text + ((char)13).ToString());
            WaitKey = "OK";
            if (!Hold(3000)) { return "error"; };

            ///////////////////////Setup Security default
            serialPort1.Write("9");
            Hold(200);
            serialPort1.Write("1");
            Hold(200);
            if (MODEL_NAME.Contains("512M"))
            {
                serialPort1.Write("oracle" + ((char)13).ToString());
                WaitKey = "OK";
                if (!Hold(3000)) { return "error"; };
                serialPort1.Write("2");
                Hold(200);
                serialPort1.Write("ns2000" + ((char)13).ToString());
                WaitKey = "OK";
                if (!Hold(3000)) { return "error"; };
            }
            else if (MODEL_NAME.Contains("256M"))
            {
                serialPort1.Write("atop" + ((char)13).ToString());
                WaitKey = "OK";
                if (!Hold(3000)) { return "error"; };
                serialPort1.Write("2");
                Hold(200);
                serialPort1.Write("2261" + ((char)13).ToString());
                WaitKey = "OK";
                if (!Hold(3000)) { return "error"; };
            }
            // exit security setting
            serialPort1.Write("0");
            Hold(200);

            /////////////////////// Reload default value
            serialPort1.Write("r");
            Hold(3000);

            /////////////////////// Reboot
            serialPort1.Write("0");

            WaitKey = "login";

            mac_str = Convert.ToString((mac_int + 6), 16);    //十進制轉十六進制,Convert.ToString(166, 16));
            if (字首零的判斷.Substring(0, 1) == "0") { mac_str = "0" + mac_str; }
            txt_mac1.Text = mac_str.ToUpper();
            txt_mac2.Text = string.Empty;
            txt_mac3.Text = string.Empty;
            txt_mac4.Text = string.Empty;
            txt_mac5.Text = string.Empty;
            txt_mac6.Text = string.Empty;
            return "Manufactory Settings successful !";
        }

        private string Set_SE7416()
        {
            int i;
            Int32 mac_int;
            string mac_str;
            string MAC1, MAC2, MAC3, MAC4;
            string 字首零的判斷 = txt_mac1.Text;
            // 進制轉換 http://aabbc1122.blog.163.com/blog/static/57043257201211331433715/
            Console.WriteLine(txt_mac1.Text.Substring(0, 6));
            mac_int = Convert.ToInt32(txt_mac1.Text.Substring(0, 6), 16);   // 十六進制轉十進制,Convert.ToInt32("CC", 16));
            // mac1
            mac_str = Convert.ToString(mac_int, 16);    // 十進制轉十六進制,Convert.ToString(166, 16));
            if (字首零的判斷.Substring(0, 1) == "0") { mac_str = "0" + mac_str; }
            txt_mac1.Text = mac_str.ToUpper();
            MAC1 = String.Format("00:60:e9:{0}:{1}:{2}", txt_mac1.Text.Substring(0, 2), txt_mac1.Text.Substring(2, 2), txt_mac1.Text.Substring(4, 2));
            Mac_forExcel = MAC1;
            // mac2
            mac_str = Convert.ToString((mac_int + 1), 16);    // 十進制轉十六進制,Convert.ToString(166, 16));
            if (字首零的判斷.Substring(0, 1) == "0") { mac_str = "0" + mac_str; }
            txt_mac2.Text = mac_str.ToUpper();
            MAC2 = String.Format("00:60:e9:{0}:{1}:{2}", txt_mac2.Text.Substring(0, 2), txt_mac2.Text.Substring(2, 2), txt_mac2.Text.Substring(4, 2));
            // mac3
            mac_str = Convert.ToString((mac_int + 2), 16);    // 十進制轉十六進制,Convert.ToString(166, 16));
            if (字首零的判斷.Substring(0, 1) == "0") { mac_str = "0" + mac_str; }
            txt_mac3.Text = mac_str.ToUpper();
            MAC3 = String.Format("00:60:e9:{0}:{1}:{2}", txt_mac3.Text.Substring(0, 2), txt_mac3.Text.Substring(2, 2), txt_mac3.Text.Substring(4, 2));
            // mac4
            mac_str = Convert.ToString((mac_int + 3), 16);    // 十進制轉十六進制,Convert.ToString(166, 16));
            if (字首零的判斷.Substring(0, 1) == "0") { mac_str = "0" + mac_str; }
            txt_mac4.Text = mac_str.ToUpper();
            MAC4 = String.Format("00:60:e9:{0}:{1}:{2}", txt_mac4.Text.Substring(0, 2), txt_mac4.Text.Substring(2, 2), txt_mac4.Text.Substring(4, 2));

            ///////////////////////Set MAC address
            // MAC1 address
            serialPort1.Write("1");
            Hold(200);
            serialPort1.Write(MAC1 + ((char)13).ToString());
            Hold(200);

            // MAC2 address
            serialPort1.Write("2");
            Hold(200);
            serialPort1.Write(MAC2 + ((char)13).ToString());
            Hold(200);

            // MAC3 address
            serialPort1.Write("3");
            Hold(200);
            serialPort1.Write(MAC3 + ((char)13).ToString());
            Hold(200);

            // MAC4 address
            serialPort1.Write("4");
            Hold(200);
            serialPort1.Write(MAC4 + ((char)13).ToString());
            Hold(200);

            ///////////////////////Set LAN address
            for (i = 5; i <= 8; i++)
            {
                // LANi
                serialPort1.Write(Convert.ToString(i));
                Hold(200);
                // IP
                serialPort1.Write("1");
                Hold(200);
                serialPort1.Write("192.168." + Convert.ToString(i - 5) + ".1" + ((char)13).ToString());
                Hold(200);

                // Netmask
                serialPort1.Write("2");
                Hold(200);
                serialPort1.Write("255.255.255.0" + ((char)13).ToString());
                Hold(200);

                // Gateway
                serialPort1.Write("3");
                Hold(200);
                serialPort1.Write("192.168." + Convert.ToString(i - 5) + ".254" + ((char)13).ToString());
                Hold(200);

                // Mode
                serialPort1.Write("4");
                Hold(200);
                serialPort1.Write("0" + ((char)13).ToString());
                Hold(200);

                // exit LANi setting
                serialPort1.Write("0");
                Hold(200);
            }

            ///////////////////////Setup Routing Netmask default
            serialPort1.Write("9");
            Hold(200);
            for (i = 1; i <= 4; i++)
            {
                // Routing Netmask i
                serialPort1.Write(Convert.ToString(i));
                Hold(200);
                serialPort1.Write("0.0.0.0" + ((char)13).ToString());
                Hold(200);
            }
            // exit Routing Netmask setting
            serialPort1.Write("0");
            Hold(200);

            ///////////////////////Setup DNS default
            serialPort1.Write("a");
            Hold(200);
            for (i = 1; i <= 4; i++)
            {
                // DNS i
                serialPort1.Write(Convert.ToString(i));
                Hold(200);
                serialPort1.Write("0.0.0.0" + ((char)13).ToString());
                Hold(200);
            }
            // exit DNS setting
            serialPort1.Write("0");
            Hold(200);

            ///////////////////////Setup Download port
            serialPort1.Write("b");
            Hold(200);
            serialPort1.Write("65535" + ((char)13).ToString());
            Hold(200);

            ///////////////////////Setup Magic code
            serialPort1.Write("c");
            Hold(200);
            serialPort1.Write("ATOP" + ((char)13).ToString());
            Hold(200);

            /////////////////////// Reload default value
            serialPort1.Write("r");
            Hold(20000);

            /////////////////////// Reboot
            serialPort1.Write("0");

            ///////////////////////Setup Security default
            //WaitKey = "Press ESC key";
            do
            {
                serialPort1.Write(((char)27).ToString());   //Esc
                WaitKey = ":TFTP";
                Hold(500);
            } while (WAIT);
            serialPort1.Write("7");
            Hold(200);
            serialPort1.Write("1");
            Hold(200);
            serialPort1.Write("atop" + ((char)13).ToString());
            Hold(200);
            serialPort1.Write("2");
            Hold(200);
            serialPort1.Write("2261" + ((char)13).ToString());
            Hold(200);
            // exit security setting
            serialPort1.Write("0");
            Hold(200);

            /////////////////////// Reboot
            serialPort1.Write("0");
            Hold(500);

            mac_str = Convert.ToString((mac_int + 4), 16);    //十進制轉十六進制,Convert.ToString(166, 16));
            if (字首零的判斷.Substring(0, 1) == "0") { mac_str = "0" + mac_str; }
            txt_mac1.Text = mac_str.ToUpper();
            txt_mac2.Text = string.Empty;
            txt_mac3.Text = string.Empty;
            txt_mac4.Text = string.Empty;
            return "Manufactory Settings successful !";
        }

        private string Set_SE7116()
        {
            //string strWait = string.Empty;

            #region MAC , 16進制與10進制轉換

            int i;
            Int32 mac_int;
            string mac_str;
            string MAC1, MAC2, MAC3, MAC4, MAC5, MAC6, MAC7, MAC8;
            string 字首零的判斷 = txt_mac1.Text;
            // 進制轉換 http://aabbc1122.blog.163.com/blog/static/57043257201211331433715/
            Console.WriteLine(txt_mac1.Text.Substring(0, 6));
            mac_int = Convert.ToInt32(txt_mac1.Text.Substring(0, 6), 16);   // 十六進制轉十進制,Convert.ToInt32("CC", 16));
            // mac1
            mac_str = Convert.ToString(mac_int, 16);    // 十進制轉十六進制,Convert.ToString(166, 16));
            if (字首零的判斷.Substring(0, 1) == "0") { mac_str = "0" + mac_str; }
            txt_mac1.Text = mac_str.ToUpper();
            MAC1 = String.Format("00:60:e9:{0}:{1}:{2}", txt_mac1.Text.Substring(0, 2), txt_mac1.Text.Substring(2, 2), txt_mac1.Text.Substring(4, 2));
            Mac_forExcel = MAC1;
            // mac2
            mac_str = Convert.ToString((mac_int + 1), 16);    // 十進制轉十六進制,Convert.ToString(166, 16));
            if (字首零的判斷.Substring(0, 1) == "0") { mac_str = "0" + mac_str; }
            txt_mac2.Text = mac_str.ToUpper();
            MAC2 = String.Format("00:60:e9:{0}:{1}:{2}", txt_mac2.Text.Substring(0, 2), txt_mac2.Text.Substring(2, 2), txt_mac2.Text.Substring(4, 2));
            // mac3
            mac_str = Convert.ToString((mac_int + 2), 16);    // 十進制轉十六進制,Convert.ToString(166, 16));
            if (字首零的判斷.Substring(0, 1) == "0") { mac_str = "0" + mac_str; }
            txt_mac3.Text = mac_str.ToUpper();
            MAC3 = String.Format("00:60:e9:{0}:{1}:{2}", txt_mac3.Text.Substring(0, 2), txt_mac3.Text.Substring(2, 2), txt_mac3.Text.Substring(4, 2));
            // mac4
            mac_str = Convert.ToString((mac_int + 3), 16);    // 十進制轉十六進制,Convert.ToString(166, 16));
            if (字首零的判斷.Substring(0, 1) == "0") { mac_str = "0" + mac_str; }
            txt_mac4.Text = mac_str.ToUpper();
            MAC4 = String.Format("00:60:e9:{0}:{1}:{2}", txt_mac4.Text.Substring(0, 2), txt_mac4.Text.Substring(2, 2), txt_mac4.Text.Substring(4, 2));
            // mac5
            mac_str = Convert.ToString((mac_int + 4), 16);    // 十進制轉十六進制,Convert.ToString(166, 16));
            if (字首零的判斷.Substring(0, 1) == "0") { mac_str = "0" + mac_str; }
            txt_mac5.Text = mac_str.ToUpper();
            MAC5 = String.Format("00:60:e9:{0}:{1}:{2}", txt_mac5.Text.Substring(0, 2), txt_mac5.Text.Substring(2, 2), txt_mac5.Text.Substring(4, 2));
            // mac6
            mac_str = Convert.ToString((mac_int + 5), 16);    // 十進制轉十六進制,Convert.ToString(166, 16));
            if (字首零的判斷.Substring(0, 1) == "0") { mac_str = "0" + mac_str; }
            txt_mac6.Text = mac_str.ToUpper();
            MAC6 = String.Format("00:60:e9:{0}:{1}:{2}", txt_mac6.Text.Substring(0, 2), txt_mac6.Text.Substring(2, 2), txt_mac6.Text.Substring(4, 2));
            // mac7
            mac_str = Convert.ToString((mac_int + 6), 16);    // 十進制轉十六進制,Convert.ToString(166, 16));
            if (字首零的判斷.Substring(0, 1) == "0") { mac_str = "0" + mac_str; }
            txt_mac7.Text = mac_str.ToUpper();
            MAC7 = String.Format("00:60:e9:{0}:{1}:{2}", txt_mac7.Text.Substring(0, 2), txt_mac7.Text.Substring(2, 2), txt_mac7.Text.Substring(4, 2));
            // mac8
            mac_str = Convert.ToString((mac_int + 7), 16);    // 十進制轉十六進制,Convert.ToString(166, 16));
            if (字首零的判斷.Substring(0, 1) == "0") { mac_str = "0" + mac_str; }
            txt_mac8.Text = mac_str.ToUpper();
            MAC8 = String.Format("00:60:e9:{0}:{1}:{2}", txt_mac8.Text.Substring(0, 2), txt_mac8.Text.Substring(2, 2), txt_mac8.Text.Substring(4, 2));

            #endregion MAC , 16進制與10進制轉換

            ///////////////////////Set MAC address
            serialPort1.Write("1");
            WaitKey = ((char)10).ToString() + ":";
            if (!Hold(1000)) { return "error"; };
            //Hold(200);
            // MAC1 address
            serialPort1.Write("1");
            Hold(200);
            serialPort1.Write(MAC1 + ((char)13).ToString());
            WaitKey = "OK";
            if (!Hold(3000)) { return "error"; };
            // MAC2 address
            serialPort1.Write("2");
            Hold(200);
            serialPort1.Write(MAC2 + ((char)13).ToString());
            WaitKey = "OK";
            if (!Hold(3000)) { return "error"; };
            // MAC3 address
            serialPort1.Write("3");
            Hold(200);
            serialPort1.Write(MAC3 + ((char)13).ToString());
            WaitKey = "OK";
            if (!Hold(3000)) { return "error"; };
            // MAC4 address
            serialPort1.Write("4");
            Hold(200);
            serialPort1.Write(MAC4 + ((char)13).ToString());
            WaitKey = "OK";
            if (!Hold(3000)) { return "error"; };
            // MAC5 address
            serialPort1.Write("5");
            Hold(200);
            serialPort1.Write(MAC5 + ((char)13).ToString());
            WaitKey = "OK";
            if (!Hold(3000)) { return "error"; };
            // MAC6 address
            serialPort1.Write("6");
            Hold(200);
            serialPort1.Write(MAC6 + ((char)13).ToString());
            WaitKey = "OK";
            if (!Hold(3000)) { return "error"; };
            // MAC7 address
            serialPort1.Write("7");
            Hold(200);
            serialPort1.Write(MAC7 + ((char)13).ToString());
            WaitKey = "OK";
            if (!Hold(3000)) { return "error"; };
            // MAC8 address
            serialPort1.Write("8");
            Hold(200);
            serialPort1.Write(MAC8 + ((char)13).ToString());
            WaitKey = "OK";
            if (!Hold(3000)) { return "error"; };
            // exit MAC setting
            serialPort1.Write("0");
            Hold(200);

            ///////////////////////Set LAN address
            serialPort1.Write("2");
            Hold(200);
            for (i = 1; i <= 8; i++)
            {
                // LANi
                serialPort1.Write(Convert.ToString(i));
                Hold(200);
                // IP
                serialPort1.Write("1");
                Hold(200);
                serialPort1.Write("100.100.10" + Convert.ToString(i - 1) + ".17" + ((char)13).ToString());
                Hold(200);
                //WaitKey = "OK";
                //if (!Hold(3000)) { return "error"; };
                // Netmask
                serialPort1.Write("2");
                Hold(200);
                serialPort1.Write("255.255.255.0" + ((char)13).ToString());
                Hold(200);
                //WaitKey = "OK";
                //if (!Hold(3000)) { return "error"; };
                // Gateway
                serialPort1.Write("3");
                Hold(200);
                serialPort1.Write("100.100.10" + Convert.ToString(i - 1) + ".254" + ((char)13).ToString());
                Hold(200);
                //WaitKey = "OK";
                //if (!Hold(3000)) { return "error"; };
                // Mode
                serialPort1.Write("4");
                Hold(200);
                serialPort1.Write("0");
                Hold(200);
                //WaitKey = "OK";
                //if (!Hold(3000)) { return "error"; };
                // Speed
                serialPort1.Write("5");
                Hold(200);
                serialPort1.Write("0");
                Hold(200);
                //WaitKey = "OK";
                //if (!Hold(3000)) { return "error"; };
                // Medium
                serialPort1.Write("6");
                Hold(200);
                if (i <= 2)
                {
                    if (MODEL_NAME.Contains("COPPER"))
                    {
                        serialPort1.Write("0");
                    }
                    else if (MODEL_NAME.Contains("FIBER"))
                    {
                        serialPort1.Write("1");
                    }
                    else
                    {
                        MessageBox.Show("MODEL_NAME 無法判斷是 'Copper' 還是 'Fiber' ", "");
                        return "error";
                    }
                }
                else
                {
                    serialPort1.Write("0");
                }
                Hold(200);
                //WaitKey = "OK";
                //if (!Hold(3000)) { return "error"; };
                // exit LANi setting
                serialPort1.Write("0");
                //Hold(200);
                WaitKey = "Ok";
                if (!Hold(3000)) { return "error"; };
            }
            // exit LAN setting
            serialPort1.Write("0");
            Hold(200);

            ///////////////////////Setup Routing Netmask default
            serialPort1.Write("3");
            Hold(200);
            for (i = 1; i <= 8; i++)
            {
                // Routing Netmask i
                serialPort1.Write(Convert.ToString(i));
                Hold(200);
                serialPort1.Write("0.0.0.0" + ((char)13).ToString());
                Hold(200);
                //WaitKey = "OK";
                //if (!Hold(3000)) { return "error"; };
            }
            // exit Routing Netmask setting
            serialPort1.Write("0");
            //Hold(200);
            WaitKey = ((char)10).ToString() + ":";
            if (!Hold(3000)) { return "error"; };

            ///////////////////////Setup DNS default
            serialPort1.Write("4");
            Hold(200);
            for (i = 1; i <= 8; i++)
            {
                // DNS i
                serialPort1.Write(Convert.ToString(i));
                Hold(200);
                serialPort1.Write("0.0.0.0" + ((char)13).ToString());
                Hold(200);
                //WaitKey = "OK";
                //if (!Hold(3000)) { return "error"; };
            }
            // exit DNS setting
            serialPort1.Write("0");
            //Hold(200);
            WaitKey = ((char)10).ToString() + ":";
            if (!Hold(3000)) { return "error"; };

            ///////////////////////Setup Download port
            serialPort1.Write("5");
            Hold(200);
            serialPort1.Write("65535" + ((char)13).ToString());
            WaitKey = "OK";
            if (!Hold(3000)) { return "error"; };

            ///////////////////////Setup Magic code
            serialPort1.Write("6");
            Hold(200);
            serialPort1.Write("ATOP" + ((char)13).ToString());
            WaitKey = "OK";
            if (!Hold(3000)) { return "error"; };

            ///////////////////////Setup Model name
            serialPort1.Write("7");
            Hold(200);
            serialPort1.Write(model_name + ((char)13).ToString());
            WaitKey = "OK";
            if (!Hold(3000)) { return "error"; };

            ///////////////////////Setup Device name
            serialPort1.Write("8");
            Hold(200);
            serialPort1.Write("0060e9" + txt_mac1.Text + ((char)13).ToString());
            WaitKey = "OK";
            if (!Hold(3000)) { return "error"; };

            ///////////////////////Setup Security default
            serialPort1.Write("9");
            Hold(200);
            serialPort1.Write("1");
            Hold(200);
            serialPort1.Write("oracle" + ((char)13).ToString());
            WaitKey = "OK";
            if (!Hold(3000)) { return "error"; };
            serialPort1.Write("2");
            Hold(200);
            serialPort1.Write("ns2000" + ((char)13).ToString());
            WaitKey = "OK";
            if (!Hold(3000)) { return "error"; };
            // exit security setting
            serialPort1.Write("0");
            Hold(200);

            /////////////////////// Reload default value
            serialPort1.Write("r");
            Hold(3000);

            /////////////////////// Reboot
            serialPort1.Write("0");

            WaitKey = "login";

            mac_str = Convert.ToString((mac_int + 8), 16);    //十進制轉十六進制,Convert.ToString(166, 16));
            if (字首零的判斷.Substring(0, 1) == "0") { mac_str = "0" + mac_str; }
            txt_mac1.Text = mac_str.ToUpper();
            txt_mac2.Text = string.Empty;
            txt_mac3.Text = string.Empty;
            txt_mac4.Text = string.Empty;
            txt_mac5.Text = string.Empty;
            txt_mac6.Text = string.Empty;
            txt_mac7.Text = string.Empty;
            txt_mac8.Text = string.Empty;
            return "Manufactory Settings successful !";
        }

        private string Set_SE4116()
        {
            //string strWait = string.Empty;

            #region MAC , 16進制與10進制轉換

            int i;
            Int32 mac_int;
            string mac_str;
            string MAC1, MAC2, MAC3, MAC4, MAC5, MAC6, MAC7, MAC8, MAC9, MAC10, MAC11, MAC12, MAC13, MAC14;
            string 字首零的判斷 = txt_mac1.Text;
            // 進制轉換 http://aabbc1122.blog.163.com/blog/static/57043257201211331433715/
            Console.WriteLine(txt_mac1.Text.Substring(0, 6));
            mac_int = Convert.ToInt32(txt_mac1.Text.Substring(0, 6), 16);   // 十六進制轉十進制,Convert.ToInt32("CC", 16));
            // mac1
            mac_str = Convert.ToString(mac_int, 16);    // 十進制轉十六進制,Convert.ToString(166, 16));
            if (字首零的判斷.Substring(0, 1) == "0") { mac_str = "0" + mac_str; }
            txt_mac1.Text = mac_str.ToUpper();
            MAC1 = String.Format("00:60:e9:{0}:{1}:{2}", txt_mac1.Text.Substring(0, 2), txt_mac1.Text.Substring(2, 2), txt_mac1.Text.Substring(4, 2));
            Mac_forExcel = MAC1;
            // mac2
            mac_str = Convert.ToString((mac_int + 1), 16);    // 十進制轉十六進制,Convert.ToString(166, 16));
            if (字首零的判斷.Substring(0, 1) == "0") { mac_str = "0" + mac_str; }
            txt_mac2.Text = mac_str.ToUpper();
            MAC2 = String.Format("00:60:e9:{0}:{1}:{2}", txt_mac2.Text.Substring(0, 2), txt_mac2.Text.Substring(2, 2), txt_mac2.Text.Substring(4, 2));
            // mac3
            mac_str = Convert.ToString((mac_int + 2), 16);    // 十進制轉十六進制,Convert.ToString(166, 16));
            if (字首零的判斷.Substring(0, 1) == "0") { mac_str = "0" + mac_str; }
            txt_mac3.Text = mac_str.ToUpper();
            MAC3 = String.Format("00:60:e9:{0}:{1}:{2}", txt_mac3.Text.Substring(0, 2), txt_mac3.Text.Substring(2, 2), txt_mac3.Text.Substring(4, 2));
            // mac4
            mac_str = Convert.ToString((mac_int + 3), 16);    // 十進制轉十六進制,Convert.ToString(166, 16));
            if (字首零的判斷.Substring(0, 1) == "0") { mac_str = "0" + mac_str; }
            txt_mac4.Text = mac_str.ToUpper();
            MAC4 = String.Format("00:60:e9:{0}:{1}:{2}", txt_mac4.Text.Substring(0, 2), txt_mac4.Text.Substring(2, 2), txt_mac4.Text.Substring(4, 2));
            // mac5
            mac_str = Convert.ToString((mac_int + 4), 16);    // 十進制轉十六進制,Convert.ToString(166, 16));
            if (字首零的判斷.Substring(0, 1) == "0") { mac_str = "0" + mac_str; }
            txt_mac5.Text = mac_str.ToUpper();
            MAC5 = String.Format("00:60:e9:{0}:{1}:{2}", txt_mac5.Text.Substring(0, 2), txt_mac5.Text.Substring(2, 2), txt_mac5.Text.Substring(4, 2));
            // mac6
            mac_str = Convert.ToString((mac_int + 5), 16);    // 十進制轉十六進制,Convert.ToString(166, 16));
            if (字首零的判斷.Substring(0, 1) == "0") { mac_str = "0" + mac_str; }
            txt_mac6.Text = mac_str.ToUpper();
            MAC6 = String.Format("00:60:e9:{0}:{1}:{2}", txt_mac6.Text.Substring(0, 2), txt_mac6.Text.Substring(2, 2), txt_mac6.Text.Substring(4, 2));
            // mac7
            mac_str = Convert.ToString((mac_int + 6), 16);    // 十進制轉十六進制,Convert.ToString(166, 16));
            if (字首零的判斷.Substring(0, 1) == "0") { mac_str = "0" + mac_str; }
            txt_mac7.Text = mac_str.ToUpper();
            MAC7 = String.Format("00:60:e9:{0}:{1}:{2}", txt_mac7.Text.Substring(0, 2), txt_mac7.Text.Substring(2, 2), txt_mac7.Text.Substring(4, 2));
            // mac8
            mac_str = Convert.ToString((mac_int + 7), 16);    // 十進制轉十六進制,Convert.ToString(166, 16));
            if (字首零的判斷.Substring(0, 1) == "0") { mac_str = "0" + mac_str; }
            txt_mac8.Text = mac_str.ToUpper();
            MAC8 = String.Format("00:60:e9:{0}:{1}:{2}", txt_mac8.Text.Substring(0, 2), txt_mac8.Text.Substring(2, 2), txt_mac8.Text.Substring(4, 2));
            // mac9
            mac_str = Convert.ToString((mac_int + 8), 16);    // 十進制轉十六進制,Convert.ToString(166, 16));
            if (字首零的判斷.Substring(0, 1) == "0") { mac_str = "0" + mac_str; }
            txt_mac9.Text = mac_str.ToUpper();
            MAC9 = String.Format("00:60:e9:{0}:{1}:{2}", txt_mac9.Text.Substring(0, 2), txt_mac9.Text.Substring(2, 2), txt_mac9.Text.Substring(4, 2));
            // mac10
            mac_str = Convert.ToString((mac_int + 9), 16);    // 十進制轉十六進制,Convert.ToString(166, 16));
            if (字首零的判斷.Substring(0, 1) == "0") { mac_str = "0" + mac_str; }
            txt_mac10.Text = mac_str.ToUpper();
            MAC10 = String.Format("00:60:e9:{0}:{1}:{2}", txt_mac10.Text.Substring(0, 2), txt_mac10.Text.Substring(2, 2), txt_mac10.Text.Substring(4, 2));
            // mac11
            mac_str = Convert.ToString((mac_int + 10), 16);    // 十進制轉十六進制,Convert.ToString(166, 16));
            if (字首零的判斷.Substring(0, 1) == "0") { mac_str = "0" + mac_str; }
            txt_mac11.Text = mac_str.ToUpper();
            MAC11 = String.Format("00:60:e9:{0}:{1}:{2}", txt_mac11.Text.Substring(0, 2), txt_mac11.Text.Substring(2, 2), txt_mac11.Text.Substring(4, 2));
            // mac12
            mac_str = Convert.ToString((mac_int + 11), 16);    // 十進制轉十六進制,Convert.ToString(166, 16));
            if (字首零的判斷.Substring(0, 1) == "0") { mac_str = "0" + mac_str; }
            txt_mac12.Text = mac_str.ToUpper();
            MAC12 = String.Format("00:60:e9:{0}:{1}:{2}", txt_mac12.Text.Substring(0, 2), txt_mac12.Text.Substring(2, 2), txt_mac12.Text.Substring(4, 2));
            // mac13
            mac_str = Convert.ToString((mac_int + 12), 16);    // 十進制轉十六進制,Convert.ToString(166, 16));
            if (字首零的判斷.Substring(0, 1) == "0") { mac_str = "0" + mac_str; }
            txt_mac13.Text = mac_str.ToUpper();
            MAC13 = String.Format("00:60:e9:{0}:{1}:{2}", txt_mac13.Text.Substring(0, 2), txt_mac13.Text.Substring(2, 2), txt_mac13.Text.Substring(4, 2));
            // mac14
            mac_str = Convert.ToString((mac_int + 13), 16);    // 十進制轉十六進制,Convert.ToString(166, 16));
            if (字首零的判斷.Substring(0, 1) == "0") { mac_str = "0" + mac_str; }
            txt_mac14.Text = mac_str.ToUpper();
            MAC14 = String.Format("00:60:e9:{0}:{1}:{2}", txt_mac14.Text.Substring(0, 2), txt_mac14.Text.Substring(2, 2), txt_mac14.Text.Substring(4, 2));

            #endregion MAC , 16進制與10進制轉換

            ///////////////////////Set MAC address
            serialPort1.Write("1");
            WaitKey = ((char)10).ToString() + ":";
            if (!Hold(1000)) { return "error"; };
            //Hold(200);
            // MAC1 address
            serialPort1.Write("1");
            Hold(200);
            serialPort1.Write(MAC1 + ((char)13).ToString());
            WaitKey = "OK";
            if (!Hold(3000)) { return "error"; };
            // MAC2 address
            serialPort1.Write("2");
            Hold(200);
            serialPort1.Write(MAC2 + ((char)13).ToString());
            WaitKey = "OK";
            if (!Hold(3000)) { return "error"; };
            // MAC3 address
            serialPort1.Write("3");
            Hold(200);
            serialPort1.Write(MAC3 + ((char)13).ToString());
            WaitKey = "OK";
            if (!Hold(3000)) { return "error"; };
            // MAC4 address
            serialPort1.Write("4");
            Hold(200);
            serialPort1.Write(MAC4 + ((char)13).ToString());
            WaitKey = "OK";
            if (!Hold(3000)) { return "error"; };
            // MAC5 address
            serialPort1.Write("5");
            Hold(200);
            serialPort1.Write(MAC5 + ((char)13).ToString());
            WaitKey = "OK";
            if (!Hold(3000)) { return "error"; };
            // MAC6 address
            serialPort1.Write("6");
            Hold(200);
            serialPort1.Write(MAC6 + ((char)13).ToString());
            WaitKey = "OK";
            if (!Hold(3000)) { return "error"; };
            // MAC7 address
            serialPort1.Write("7");
            Hold(200);
            serialPort1.Write(MAC7 + ((char)13).ToString());
            WaitKey = "OK";
            if (!Hold(3000)) { return "error"; };
            // MAC8 address
            serialPort1.Write("8");
            Hold(200);
            serialPort1.Write(MAC8 + ((char)13).ToString());
            WaitKey = "OK";
            if (!Hold(3000)) { return "error"; };
            // MAC9 address
            serialPort1.Write("9");
            Hold(200);
            serialPort1.Write(MAC9 + ((char)13).ToString());
            WaitKey = "OK";
            if (!Hold(3000)) { return "error"; };
            // MAC10 address
            serialPort1.Write("a");
            Hold(200);
            serialPort1.Write(MAC10 + ((char)13).ToString());
            WaitKey = "OK";
            if (!Hold(3000)) { return "error"; };
            // MAC11 address
            serialPort1.Write("b");
            Hold(200);
            serialPort1.Write(MAC11 + ((char)13).ToString());
            WaitKey = "OK";
            if (!Hold(3000)) { return "error"; };
            // MAC12 address
            serialPort1.Write("c");
            Hold(200);
            serialPort1.Write(MAC12 + ((char)13).ToString());
            WaitKey = "OK";
            if (!Hold(3000)) { return "error"; };
            // MAC13 address
            serialPort1.Write("d");
            Hold(200);
            serialPort1.Write(MAC13 + ((char)13).ToString());
            WaitKey = "OK";
            if (!Hold(3000)) { return "error"; };
            // MAC14 address
            serialPort1.Write("e");
            Hold(200);
            serialPort1.Write(MAC14 + ((char)13).ToString());
            WaitKey = "OK";
            if (!Hold(3000)) { return "error"; };
            // exit MAC setting
            serialPort1.Write("0");
            Hold(200);

            ///////////////////////Set LAN address
            serialPort1.Write("2");
            Hold(200);
            for (i = 1; i <= 14; i++)
            {
                // LANi
                if (i < 10)
                {
                    serialPort1.Write(Convert.ToString(i));
                }
                else
                {
                    switch (i)
                    {
                        case 10:
                            serialPort1.Write("a");
                            break;
                        case 11:
                            serialPort1.Write("b");
                            break;
                        case 12:
                            serialPort1.Write("c");
                            break;
                        case 13:
                            serialPort1.Write("d");
                            break;
                        case 14:
                            serialPort1.Write("e");
                            break;
                    }
                }
                Hold(200);
                // IP
                serialPort1.Write("1");
                Hold(200);
                if (i < 11)
                {
                    serialPort1.Write("100.100.10" + Convert.ToString(i - 1) + ".17" + ((char)13).ToString());
                }
                else
                {
                    serialPort1.Write("100.100.1" + Convert.ToString(i - 1) + ".17" + ((char)13).ToString());
                }
                Hold(200);
                //WaitKey = "OK";
                //if (!Hold(3000)) { return "error"; };
                // Netmask
                serialPort1.Write("2");
                Hold(200);
                serialPort1.Write("255.255.255.0" + ((char)13).ToString());
                Hold(200);
                //WaitKey = "OK";
                //if (!Hold(3000)) { return "error"; };
                // Gateway
                serialPort1.Write("3");
                Hold(200);
                if (i < 11)
                {
                    serialPort1.Write("100.100.10" + Convert.ToString(i - 1) + ".254" + ((char)13).ToString());
                }
                else
                {
                    serialPort1.Write("100.100.1" + Convert.ToString(i - 1) + ".254" + ((char)13).ToString());
                }
                Hold(200);
                //WaitKey = "OK";
                //if (!Hold(3000)) { return "error"; };
                // Mode
                serialPort1.Write("4");
                Hold(200);
                serialPort1.Write("0");
                Hold(200);
                //WaitKey = "OK";
                //if (!Hold(3000)) { return "error"; };
                // Speed
                serialPort1.Write("5");
                Hold(200);
                serialPort1.Write("0");
                Hold(200);
                //WaitKey = "OK";
                //if (!Hold(3000)) { return "error"; };
                // Medium
                serialPort1.Write("6");
                Hold(200);
                if (i <= 2)
                {
                    if (MODEL_NAME.Contains("COPPER"))
                    {
                        serialPort1.Write("0");
                    }
                    else if (MODEL_NAME.Contains("FIBER"))
                    {
                        serialPort1.Write("1");
                    }
                    else
                    {
                        MessageBox.Show("MODEL_NAME 無法判斷是 'Copper' 還是 'Fiber' ", "");
                        return "error";
                    }
                }
                else
                {
                    serialPort1.Write("0");
                }
                Hold(200);
                //WaitKey = "OK";
                //if (!Hold(3000)) { return "error"; };
                // exit LANi setting
                serialPort1.Write("0");
                //Hold(500);
                WaitKey = "Ok";
                if (!Hold(3000)) { return "error"; };
            }
            // exit LAN setting
            serialPort1.Write("0");
            Hold(200);

            ///////////////////////Setup Routing Netmask default
            serialPort1.Write("3");
            Hold(200);
            for (i = 1; i <= 8; i++)
            {
                // Routing Netmask i
                serialPort1.Write(Convert.ToString(i));
                Hold(200);
                serialPort1.Write("0.0.0.0" + ((char)13).ToString());
                Hold(200);
                //WaitKey = "OK";
                //if (!Hold(3000)) { return "error"; };
            }
            // exit Routing Netmask setting
            serialPort1.Write("0");
            //Hold(200);
            WaitKey = ((char)10).ToString() + ":";
            if (!Hold(3000)) { return "error"; };

            ///////////////////////Setup DNS default
            serialPort1.Write("4");
            Hold(200);
            for (i = 1; i <= 8; i++)
            {
                // DNS i
                serialPort1.Write(Convert.ToString(i));
                Hold(200);
                serialPort1.Write("0.0.0.0" + ((char)13).ToString());
                Hold(200);
                //WaitKey = "OK";
                //if (!Hold(3000)) { return "error"; };
            }
            // exit DNS setting
            serialPort1.Write("0");
            //Hold(200);
            WaitKey = ((char)10).ToString() + ":";
            if (!Hold(3000)) { return "error"; };

            ///////////////////////Setup Download port
            serialPort1.Write("5");
            Hold(200);
            serialPort1.Write("65535" + ((char)13).ToString());
            WaitKey = "OK";
            if (!Hold(3000)) { return "error"; };

            ///////////////////////Setup Magic code
            serialPort1.Write("6");
            Hold(200);
            serialPort1.Write("ATOP" + ((char)13).ToString());
            WaitKey = "OK";
            if (!Hold(3000)) { return "error"; };

            ///////////////////////Setup Model name
            serialPort1.Write("7");
            Hold(200);
            serialPort1.Write(model_name + ((char)13).ToString());
            WaitKey = "OK";
            if (!Hold(3000)) { return "error"; };

            ///////////////////////Setup Device name
            serialPort1.Write("8");
            Hold(200);
            serialPort1.Write("0060e9" + txt_mac1.Text + ((char)13).ToString());
            WaitKey = "OK";
            if (!Hold(3000)) { return "error"; };

            ///////////////////////Setup Security default
            serialPort1.Write("9");
            Hold(200);
            serialPort1.Write("1");
            Hold(200);
            serialPort1.Write("oracle" + ((char)13).ToString());
            WaitKey = "OK";
            if (!Hold(3000)) { return "error"; };
            serialPort1.Write("2");
            Hold(200);
            serialPort1.Write("ns2000" + ((char)13).ToString());
            WaitKey = "OK";
            if (!Hold(3000)) { return "error"; };
            // exit security setting
            serialPort1.Write("0");
            Hold(200);

            /////////////////////// Reload default value
            serialPort1.Write("r");
            Hold(3000);

            /////////////////////// Reboot
            serialPort1.Write("0");

            WaitKey = "login";

            mac_str = Convert.ToString((mac_int + 8), 16);    //十進制轉十六進制,Convert.ToString(166, 16));
            if (字首零的判斷.Substring(0, 1) == "0") { mac_str = "0" + mac_str; }
            txt_mac1.Text = mac_str.ToUpper();
            txt_mac2.Text = string.Empty;
            txt_mac3.Text = string.Empty;
            txt_mac4.Text = string.Empty;
            txt_mac5.Text = string.Empty;
            txt_mac6.Text = string.Empty;
            txt_mac7.Text = string.Empty;
            txt_mac8.Text = string.Empty;
            txt_mac9.Text = string.Empty;
            txt_mac10.Text = string.Empty;
            txt_mac11.Text = string.Empty;
            txt_mac12.Text = string.Empty;
            txt_mac13.Text = string.Empty;
            txt_mac14.Text = string.Empty;
            return "Manufactory Settings successful !";
        }

        #endregion Bootloader setting

        #region 貓頭鷹 v1.6p (2013/11/22)

        private void MultiPortTesting_settings(string ip, int interval, string max_port, int server_port, int loopback, string duration)
        {
            int i;
            int min_port;

            if (max_port.Contains("-")) // 格式ex: 0-4、1-4
            {
                string[] port;
                port = max_port.Split(new char[] { '-' });
                min_port = Convert.ToInt32(port[0]);
                max_port = port[1];
            }
            else
            {
                min_port = 1;    // 格式ex: 4
            }

            if (File.Exists(appPATH + "\\setting.txt"))
            {
                File.Delete(appPATH + "\\setting.txt");
            }

            // 建立檔案
            FileStream fs = File.Open(appPATH + "\\setting.txt", FileMode.OpenOrCreate, FileAccess.Write);
            // 建構StreamWriter物件
            StreamWriter sw = new StreamWriter(fs);

            // 寫入
            sw.WriteLine(ip);           // IP
            sw.WriteLine("2");         // Send Lenth
            sw.WriteLine(interval);     // Send Interval
            sw.WriteLine(max_port);     // total port
            sw.WriteLine(server_port);
            sw.WriteLine(server_port);
            sw.WriteLine("5");          // timeout
            sw.WriteLine("0");          // pingpong 的設定值，在Multi-Port-Testingv1.6r.exe設定為等於loopback值
            sw.WriteLine("0");
            sw.WriteLine("0");
            sw.WriteLine("0");
            sw.WriteLine("0");
            sw.WriteLine("True");
            sw.WriteLine("False");
            sw.WriteLine("False");
            sw.WriteLine("0");
            sw.WriteLine(loopback);
            //sw.WriteLine(duration);
            sw.WriteLine("99999");
            for (i = 1; i <= 32; i++)
            {
                if (min_port <= i && i <= Convert.ToInt32(max_port))
                {
                    if (min_port == 0)
                    {
                        if (i % 2 == 1)
                        {
                            sw.WriteLine(Convert.ToString(Math.Abs(loopback - 1)));
                        }
                        else
                        {
                            sw.WriteLine(loopback);
                        }
                    }
                    else
                    {
                        sw.WriteLine("1");
                    }
                }
                else
                {
                    sw.WriteLine("0");
                }
            }

            // 清除目前寫入器(Writer)的所有緩衝區，並且造成任何緩衝資料都寫入基礎資料流
            sw.Flush();

            // 關閉目前的StreamWriter物件和基礎資料流
            sw.Close();
            fs.Close();
        }

        #endregion 貓頭鷹 v1.6p (2013/11/22)

        private float TimeUnit(int idx, int i)
        {
            string[] line;
            string tag = Convert.ToString(lblFunction[idx].Tag);
            line = tag.Split(' ');
            if (line.GetUpperBound(0) >= i + 1)
            {
                switch (line[i + 1].ToLower())
                {
                    case "s":
                        return Convert.ToInt64(line[i]) * 1;
                    case "m":
                        return Convert.ToInt64(line[i]) * 60;
                    case "h":
                        return Convert.ToInt64(line[i]) * 60 * 60;
                    case "d":
                        return Convert.ToInt64(line[i]) * 60 * 60 * 24;
                    default:
                        return Convert.ToInt64(line[i]) * 60;
                }
            }
            else { return Convert.ToInt64(line[i]) * 60; }
        }

        private void pause(double delay)
        {
            DateTime time_before = DateTime.Now;
            while (((TimeSpan)(DateTime.Now - time_before)).TotalMinutes < delay)
            {
                Application.DoEvents();
                Thread.Sleep(1000); // 至少打資料兩次
            }
        }

        private void lblSecret_Click(object sender, EventArgs e)
        {
            secretX += 1;
            if (secretX == 5)
            {
                debugMode.Visible = true;
                txt_Rx_EUT.Visible = true;
            }
        }

        private void disconnectALL_Click(object sender, EventArgs e)
        {
            int n;
            if (telnet.Connected) { telnet.Close(); }
            if (atoptestd.Connected) { atoptestd.Close(); }
            serialPort1_Close();
            serialPort2_Close();
            //if (serialPort1.IsOpen) { serialPort1.Close(); }
            //if (serialPort2.IsOpen) { serialPort2.Close(); }

            Run_Stop = true;
            WAIT = false;

            for (n = 0; n < TestFun_MaxIdx; n++)
            {
                if (lblFunction[n].Text.ToUpper().Contains("CONSOLE-DUT") || lblFunction[n].Text.ToUpper().Contains("CONSOLE-EUT") || lblFunction[n].Text.ToUpper().Contains("TELNET") || lblFunction[n].Text.ToUpper().Contains("TESTD"))
                {
                    lblFunction[n].BackColor = Color.FromArgb(255, 255, 255);
                }
            }
            lblStatus.Text = "所有的連線已經中斷";
        }

        private void 開啟Monitor_Click(object sender, EventArgs e)
        {
            Shell(appPATH, "monitor2.5.exe");
        }

        private void lanEnvironmentSetting_Click(object sender, EventArgs e)
        {
            Shell(appPATH, "LAN_setting.bat");
        }

        // http://msdn.microsoft.com/zh-cn/library/aa168292(office.11).aspx
        // 設定必要的物件
        // 按照順序分別是Application > Workbook > Worksheet > Range > Cell
        // (1) Application ：代表一個 Excel 程序。
        // (2) WorkBook ：代表一個 Excel 工作簿。
        // (3) WorkSheet ：代表一個 Excel 工作表，一個 WorkBook 包含好幾個工作表。
        // (4) Range ：代表 WorkSheet 中的多個單元格區域。
        // (5) Cell ：代表 WorkSheet 中的一個單元格。
        private void writeExcelLog()
        {
            int j;

            // 檢查路徑的資料夾是否存在，沒有則建立
            if (!Directory.Exists(@"C:\Atop_Log\ATC\" + MODEL_NAME))
            {
                Directory.CreateDirectory(@"C:\Atop_Log\ATC\" + MODEL_NAME);
            }

            // 設定儲存檔名，不用設定副檔名，系統自動判斷 excel 版本，產生 .xls 或 .xlsx 副檔名
            // C:\Atop_Log\ATC\產品名稱\年_月_工單號碼.xls
            time = DateTime.Now;
            string name = time.Year + "_" + time.Month + "_" + productNum_forExcel.ToUpper() + ".xls";
            string pathFile = @"C:\Atop_Log\ATC\" + MODEL_NAME + @"\" + name;

            Microsoft.Office.Interop.Excel.Application excelApp;
            Microsoft.Office.Interop.Excel._Workbook wBook;
            Microsoft.Office.Interop.Excel._Worksheet wSheet;
            Microsoft.Office.Interop.Excel.Range wRange;

            // 開啟一個新的應用程式
            excelApp = new Microsoft.Office.Interop.Excel.Application();
            // 讓Excel文件可見
            excelApp.Visible = false;
            // 停用警告訊息
            excelApp.DisplayAlerts = false;
            // 開啟舊檔案
            if (GetFiles(pathFile))
            {
                wBook = excelApp.Workbooks.Open(pathFile, Type.Missing, false, Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing);
            }
            // 創建一個新的工作簿
            excelApp.Workbooks.Add(Type.Missing);
            // 引用第一個活頁簿
            wBook = excelApp.Workbooks[1];
            // 設定活頁簿焦點
            wBook.Activate();
            // 設定開啟密碼
            //wBook.Password = "23242249";

            try
            {
                // 引用第一個工作表(轉型)
                wSheet = (Microsoft.Office.Interop.Excel._Worksheet)wBook.Worksheets[1];
                // 命名工作表的名稱
                wSheet.Name = "測試紀錄";
                // Worksheet.Protect 方法。保護工作表，使工作表無法修改
                wSheet.Protect("23242249", true, true, true, true, true, true, true, true, true, true, true, true, true, true, true);
                // 設定工作表焦點
                wSheet.Activate();
                // 所有儲存格 文字置中
                excelApp.Cells.HorizontalAlignment = 3;
                // 所有儲存格 自動換行
                excelApp.Cells.WrapText = true;
                // 所有儲存格格式強迫以文字來儲存
                excelApp.Cells.NumberFormat = "@";

                // 設定第1列資料
                excelApp.Cells[1, 1] = "測試人員";
                excelApp.Cells[1, 2] = "工單號碼";
                excelApp.Cells[1, 3] = "產品序號(SN)";
                excelApp.Cells[1, 4] = "產品名稱";
                excelApp.Cells[1, 5] = "MAC1";
                excelApp.Cells[1, 6] = "Kernel";
                excelApp.Cells[1, 7] = "AP";
                excelApp.Cells[1, 8] = "開始測試時間";
                excelApp.Cells[1, 9] = "結束測試時間";
                // 取得已經使用的Columns數(X軸)
                //int usedRangeColumns = wSheet.UsedRange.Columns.Count + 1;
                //for (j = usedRangeColumns; j < TEST_RESULT.Count + usedRangeColumns; j++)
                //{
                //    excelApp.Cells[1, j] = TEST_RESULT[j - usedRangeColumns];
                //}
                for (j = 10; j < TEST_FunLog.Count + 10; j++)
                {
                    excelApp.Cells[1, j] = TEST_FunLog[j - 10];
                    Debug.Print(TEST_FunLog[j - 10].ToString());
                }
                // 設定第1列顏色
                wRange = wSheet.get_Range(wSheet.Cells[1, 1], wSheet.Cells[1, TEST_FunLog.Count + 9]);
                wRange.Select();
                wRange.Font.Color = ColorTranslator.ToOle(Color.White);
                wRange.Interior.Color = ColorTranslator.ToOle(Color.DimGray);
                //wRange.Columns.AutoFit();   // 自動調整欄寬
                wRange.ColumnWidth = 15; // 設置儲存格的寬度

                // 取得已經使用的Rows數(Y軸)
                int usedRangeRows = wSheet.UsedRange.Rows.Count + 1;
                // 設定第usedRange列資料
                excelApp.Cells[usedRangeRows, 1] = tester_forExcel.ToUpper();
                excelApp.Cells[usedRangeRows, 2] = productNum_forExcel.ToUpper();
                string snTemp = string.Empty;
                if (coreSN_forExcel != string.Empty && coreSN_forExcel != null)
                {
                    snTemp = "Core:" + coreSN_forExcel;
                }
                if (lanSN_forExcel != string.Empty && lanSN_forExcel != null)
                {
                    if (snTemp == string.Empty)
                    {
                        snTemp = "Lan:" + lanSN_forExcel;
                    }
                    else
                    {
                        snTemp = snTemp + ((char)10).ToString() + "Lan:" + lanSN_forExcel;
                    }
                }
                if (uartSN_forExcel != string.Empty && uartSN_forExcel != null)
                {
                    if (snTemp == string.Empty)
                    {
                        snTemp = "Uart:" + uartSN_forExcel;
                    }
                    else
                    {
                        snTemp = snTemp + ((char)10).ToString() + "Uart:" + uartSN_forExcel;
                    }
                }
                if (serial1SN_forExcel != string.Empty && serial1SN_forExcel != null)
                {
                    if (snTemp == string.Empty)
                    {
                        snTemp = "Serial1:" + serial1SN_forExcel;
                    }
                    else
                    {
                        snTemp = snTemp + ((char)10).ToString() + "Serial1:" + serial1SN_forExcel;
                    }
                }
                if (serial2SN_forExcel != string.Empty && serial2SN_forExcel != null)
                {
                    if (snTemp == string.Empty)
                    {
                        snTemp = "Serial2:" + serial2SN_forExcel;
                    }
                    else
                    {
                        snTemp = snTemp + ((char)10).ToString() + "Serial2:" + serial2SN_forExcel;
                    }
                }
                if (serial3SN_forExcel != string.Empty && serial3SN_forExcel != null)
                {
                    if (snTemp == string.Empty)
                    {
                        snTemp = "Serial3:" + serial3SN_forExcel;
                    }
                    else
                    {
                        snTemp = snTemp + ((char)10).ToString() + "Serial3:" + serial3SN_forExcel;
                    }
                }
                if (serial4SN_forExcel != string.Empty && serial4SN_forExcel != null)
                {
                    if (snTemp == string.Empty)
                    {
                        snTemp = "Serial4:" + serial4SN_forExcel;
                    }
                    else
                    {
                        snTemp = snTemp + ((char)10).ToString() + "Serial4:" + serial4SN_forExcel;
                    }
                }
                excelApp.Cells[usedRangeRows, 3] = snTemp;
                excelApp.Cells[usedRangeRows, 4] = MODEL_NAME;
                excelApp.Cells[usedRangeRows, 5] = Mac_forExcel;
                excelApp.Cells[usedRangeRows, 6] = "";
                excelApp.Cells[usedRangeRows, 7] = "";
                excelApp.Cells[usedRangeRows, 8] = startTime;
                excelApp.Cells[usedRangeRows, 9] = endTime;
                for (j = 10; j < TEST_FunLog.Count + 10; j++)
                {
                    excelApp.Cells[usedRangeRows, j] = TEST_RESULT[j - 10];
                }

                try
                {
                    // 另存活頁簿
                    wBook.SaveAs(pathFile, Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing, Microsoft.Office.Interop.Excel.XlSaveAsAccessMode.xlNoChange, Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing);
                    MessageBox.Show("Excel log 儲存於 " + Environment.NewLine + pathFile);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("儲存檔案出錯，檔案可能正在使用" + Environment.NewLine + ex.Message);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("產生 Excel log 時出錯！" + Environment.NewLine + ex.Message);
            }

            //關閉活頁簿
            wBook.Close(false, Type.Missing, Type.Missing);

            //關閉Excel
            excelApp.Quit();

            //釋放Excel資源
            System.Runtime.InteropServices.Marshal.ReleaseComObject(excelApp);
            wBook = null;
            wSheet = null;
            wRange = null;
            excelApp = null;
            GC.Collect();
        }

        // 讀取目錄下所有檔案，並判斷指定檔案(不含副檔名)是否存在
        private bool GetFiles(string filename)
        {
            int i;
            string[] files;
            string keyword;

            files = Directory.GetFiles(@"C:\Atop_Log\ATC\" + MODEL_NAME);
            keyword = filename.Replace("C:\\Atop_Log\\ATC\\" + MODEL_NAME + "\\", string.Empty);
            for (i = 0; i < files.Length; i++)
            {
                files[i] = files[i].Replace(@"C:\Atop_Log\ATC\" + MODEL_NAME + "\\", string.Empty);
                if (files[i].Contains(keyword))
                {
                    return true;
                }
            }
            return false;
        }

        private void cmdNext_Click(object sender, EventArgs e)
        {
            int n;
            if (cmdOpeFile.Text != "檔案名稱")
            {
                InputBox inputbox = new InputBox();
                inputbox.ShowDialog();
                tester_forExcel = InputBox.tester;
                productNum_forExcel = InputBox.productNum;
                coreSN_forExcel = InputBox.coreSN;
                lanSN_forExcel = InputBox.lanSN;
                uartSN_forExcel = InputBox.uartSN;
                serial1SN_forExcel = InputBox.serial1SN;
                serial2SN_forExcel = InputBox.serial2SN;
                serial3SN_forExcel = InputBox.serial3SN;
                serial4SN_forExcel = InputBox.serial4SN;

                time = DateTime.Now;
                endTime = String.Format("{0:00}/{1:00}" + ((char)10).ToString() + "{2:00}:{3:00}:{4:00}", time.Month, time.Day, time.Hour, time.Minute, time.Second);

                if (InputBox.CancelButton == false)
                {
                    writeExcelLog();
                }

                Shell(appPATH, "arp-d.bat");
                if (telnet.Connected) { telnet.Close(); }
                serialPort1_Close();
                serialPort2_Close();

                Test_Idx = 0;
                Run_Stop = true;
                WAIT = false;
                txt_Rx.Text = string.Empty;
                for (n = 0; n < TestFun_MaxIdx; n++)
                {
                    lblFunction[n].BackColor = Color.FromArgb(255, 255, 255);
                }
                for (n = 0; n < TEST_RESULT.Length; n++)
                {
                    TEST_RESULT[n] = string.Empty;
                }
            }
        }

        private void txtDutIP_TextChanged(object sender, EventArgs e)
        {
            if (IsIP(txtDutIP.Text))
            {
                TARGET_IP = txtDutIP.Text;
                txtDutIP.ForeColor = Color.Green;
            }
            else
            {
                txtDutIP.ForeColor = Color.Red;
            }
        }

        private void txtEutIP_TextChanged(object sender, EventArgs e)
        {
            if (IsIP(txtEutIP.Text))
            {
                TARGET_eutIP = txtEutIP.Text;
                txtEutIP.ForeColor = Color.Green;
            }
            else
            {
                txtEutIP.ForeColor = Color.Red;
            }
        }

        private void enterTmr_Tick(object sender, EventArgs e)
        {
            SendCmd("");
        }

        #region 終端機指令

        private void 列出jffs2內容_Click(object sender, EventArgs e)
        {
            SendCmd("ls -l /jffs2/");
        }

        private void 磁碟空間使用情況ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SendCmd("df -h");
        }

        private void 當前系統運行的程序ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SendCmd("ps aux");
        }

        #endregion 終端機指令
    }
}

/*
 -----選取 columns-----
 xlWs.columns("H").select	'選取單行
 xlWs.columns("E:H").select	'選取連續行
 xlWs.columns("E:E,H:H")	'選取多行
 xlWs.range("E:E,G:G").select	'用range選取多行
 xlWs.columns.select	'選取全部行 = 全選
 -----用數字選取 columns-----
 xlWs.columns(3).select	'選取第3行
 xlWs.columns(i).select	'單選第i行

 xlWs.columns(i).columnwidth = 5	'第i行的欄寬=5
 xlWs.range("C:C,E:E,G:G").columnwidth = 5
 xlWs.columns(i).AutoFit	'第i行的欄寬=最適欄寛
 xlWs.columns("D:F").delete	'刪除行
 xlWs.range("C:C,E:E,G:G").delete	'刪除行

 -----選取 rows-----
 xlWs.rows(i).select	'選取單列
 xlWs.rows("2:6").select	'選取連續列
 xlWs.rows.select	'選取全部列 = 全選
 xlWs.range("3:3, 5:5, 8:8").select	'選取多列

 xlWs.rows(3).rowheight = 5	'列高
 xlWs.rows(3).insert	'插入列
 xlWs.rows(3).delete	'刪除列

 -----選取 cells-----
 xlWs.range("D4:D4").select	'選取單格
 xlWs.range("B2:H6").select	'選取範圍
 xlWs.range("D2:B5, F8:I9").select	'選取多個範圍

 xlWs.range("D4") = "TEST"	'儲存格內容
 xlWs.range("D4").font.name = "cambria"	'設定字型
 xlWs.range("D4").font.size = 24	'設定字體
 xlWs.range("D4").font.bold = true	'粗體
 xlWs.range("D4").font.color = vbblue	'設定文字顏色
 xlWs.range("D4").Interior.colorindex = 36	'設定背景顏色

 -----合併儲存格-----
 xlWs.range("E5:I6").mergecells = true	'合併儲存格
 tstring = "E" & i & ":" & "I" & j
 xlWs.range(tstring).mergecells = true	'合併儲存格

 -----儲存格對齊-----
 xlWs.range("D4").verticalalignment = 2	'上下對齊
 1=靠上 , 2=置中 , 3=靠下 , 4=垂直對齊??
 xlWs.range("D4").horizontalalignment = 1	'左右對齊
 1=一般 , 2=置左 , 3=置中 , 4=靠右 , 5=填滿 , 6=水平對齊? , 7=跨欄置中

 -----儲存格框線-----
 xlWs.range("D4").borders(n)	'框線方向
 n= 1:左, 2:右, 3:上, 4:下, 5:斜, 6:斜
 xlWs.range("D4").borders(4).color = 5
 xlWs.range("D4").borders(4).weight = 3	'框線粗細
 xlWs.range("D4").borders(4).linestyle = 1	'框線樣式
 線種類= 1,7:細實 2:細虛 4:一點虛 9:雙細實線
 xlWs.range("D4").borders(4).color = 6

 -----儲存格計算-----
 xlWs.range("I17").value = 20
 xlWs.range("I18").value = 30
 xlWs.Range("I19").Formula = xlWs.Range("I17") * xlWs.Range("I18") / 100
 xlWs.Range("I20").Formula = "=SUM(I17:I19)"

 -----加入註解-----
 xlWs.cells(n,1).AddComment
 xlWs.cells(n,1).Comment.visible = False
 xlWs.cells(n,1).Comment.text("有建BOM表,卻不計算BOM的成本")
 -----讀取註解,待測-----
 comment-text = xlWs.cells(n,1).Comment.text()
 comment-text = xlWs.cells(n,1).Comment.text

 -----列出 excel 字體顏色 color values-----
 for i = 1 to 56
 xlWs.cells(i + 3, 1).value = "value = " & i
 xlWs.cells(i + 3, 2).interior.colorindex = i
 next
*/