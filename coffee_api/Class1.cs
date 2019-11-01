using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Ports;

using System.Timers;
using System.Xml.Schema;
using System.Text.RegularExpressions;
using System.Collections;


namespace coffee_api
{
    //
    //public class MegaCoffee_api
    public class Class1
    {
        private string localproductid;
        internal static readonly int TIMER_TICK_MS = 100;
        private string m_serial_port_number = "com5";
        private int m_serial_port_baud_rate = 115200;
        private static SerialPort m_serial_port = new SerialPort();
        private static byte[] m_rx_buf;
        private static int m_rx_buf_rx_index;
        private static long m_rx_chunk_timestamp;
        private List<byte> buffer = new List<byte>(4096);
        private static byte[] ReceiveBytes = new byte[4096];
        public Boolean dataflag = false;
        List<System.String> datalist = new List<System.String>();
        ArrayList datalist1 = new ArrayList();
        public ArrayList id_list = new ArrayList();
        public ArrayList name_list = new ArrayList();
        public ArrayList msg_list = new ArrayList();
        public Boolean productlistAvailable = false;
        List<byte> arraydata = new List<byte>();
        List<byte> dealdata = new List<byte>();
        List<byte> arraydata_after_escape = new List<byte>();
        List<byte> send_data_array = new List<byte>();
        Boolean start_flag = false;
        Boolean end_flag = false;

        public volatile Boolean serial_status = false;
        public volatile int ResponseCode;
        private volatile int _product_status;
        public volatile uint msg_code;

        Object locker = new Object();
        Object lockerfile = new Object();
        public int product_status
        {
            set {
                lock (locker)
                {
                    _product_status = value;
                }
            }
            get {
                int v;
                lock (locker)
                {
                    v = _product_status;
                }
                return v;
            }
        }
        /// <summary>
        /// read one to clear
        /// </summary>
        /// <returns></returns>
        private void save_log(string input)
        {
            lock (lockerfile) {
                try {
                    StreamWriter fileWriter1;
                    string log_file = DateTime.Now.ToString("yyyy-MM-dd") + "_coffee_log.txt";
                    fileWriter1 = new StreamWriter(log_file, true, Encoding.Default);
                    fileWriter1.WriteLine(DateTime.Now.ToString() + ":" + input);
                    fileWriter1.Close();
                } catch { }
                
            }
        }
        private int rocStatus( )
        {
            int v;
            lock (locker)
            {
                v = _product_status;
                if (v == 1)
                { _product_status = 0; }
            }
            return v;
        }

        public int clear_product_status()
        {
            int v;
            lock (locker)
            {
                v = product_status;
                if (v == 1)
                { product_status = 0; }
            }
            return v;
        }


        public struct coffee_machine_status
        {
            public byte BoilerControllerOK;
            public byte StatusManagerOK;
            public byte BeanHopperPresent;
            public byte BeanHopperLevelOK;
            public byte GroundsDrawerOK;
            public byte MilkTemperatureOK;
            public byte MilkLevelOK;
            public byte CleaningOK;
            public byte PistonError;
            public byte CommunicationError;
            public byte ComponentError;
            public byte ConfigurationError;
            public byte FlowError;
            public byte BrewChamberEmpty;
            public byte BrewChamberTooFull;
            public byte MilkEmptyDuringProduct;
            public byte SafetyReedDuringProduct;
            public byte ChassisFanFailure;
        }
        const UInt16 Polynom = 0xC86C;
        public coffee_machine_status coffee_machine_status_t;

        UInt16 crc16(byte[] data, int length)
        {
            UInt16 crc = 0xffff;
            for (int i = 0; i < length; i++)
            {
                crc = (UInt16)(crc ^ (((UInt16)data[i]) << 8));
                for (int j = 0; j < 8; ++j)
                {
                    if ((crc & 0x8000) == 0x8000)
                        crc = (UInt16)(((UInt16)(crc << 1)) ^ Polynom);
                    else
                        crc = (UInt16)(crc << 1);
                }
            }
            return crc;
        }

        public bool read_product_status()
        {
            if (product_status == 1)
            {
                product_status = 0;
                return true;
            }
            else {
                return false;
            }
        }
        UInt16 append_crc16(byte[] data, int length)
        {
            UInt16 crc = crc16(data, length - 2);

            data[length - 2] = (byte)((crc >> 8) & 0xff);
            data[length - 1] = (byte)(crc & 0xff);
            return crc;
        }

        public Class1()
        {

           //构造函数，初始化对象
        }

        private void SerialDataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            Boolean endflag = false;

            //接收数据         
            do
            {
                int count = m_serial_port.BytesToRead;
                if (count <= 0)
                    break;
                byte[] readBuffer = new byte[count];
                m_serial_port.Read(readBuffer, 0, count);

                for (int i = 0; i < count; i++)
                {
                    arraydata.Add(readBuffer[i]);
                }
            } while (m_serial_port.BytesToRead > 0);
            //Console.WriteLine(arraydata.Count);
            //return;

            int start_position = 0;
            int end_position = 0;
            for (int i = 0; i < arraydata.Count; i++)
            {
                if (arraydata[i] == 0x02)
                {
                    start_position = i;
                    start_flag = true;
                }
                if ((arraydata[i] == 0x03) && (start_flag == true))
                {
                    end_position = i;
                    end_flag = true;
                }
                if ((end_flag == true) && (start_flag == true))//开始处理数据
                {
                    for (int ii = start_position; ii <= end_position; ii++)//去除转义字符
                    {
                        dealdata.Add(arraydata[ii]);
                        if (arraydata[ii] == 0x10)
                        {
                            switch (arraydata[ii + 1])
                            {
                                case 0x22:
                                    arraydata_after_escape.Add(0x02);
                                    break;
                                case 0x23:
                                    arraydata_after_escape.Add(0x03);
                                    break;
                                case 0x30:
                                    arraydata_after_escape.Add(0x10);
                                    break;
                                default:
                                    Console.WriteLine("strange data");
                                    break;
                            }
                            ii++;
                        }
                        else
                        {
                            arraydata_after_escape.Add(arraydata[ii]);
                        }
                    }

                    //crc校验，并取数据
                    int length = arraydata_after_escape.Count - 2;
                    if (length<4) break;
                    byte[] senddata = new byte[length];
                    byte[] senddata1 = new byte[length - 2];

                    for (int j = 0; j < arraydata_after_escape.Count - 4; j++)
                    {
                        senddata[j] = arraydata_after_escape[j + 1];
                    }
                    UInt16 tmpcrc = append_crc16(senddata, length);
                    UInt16 receive_crc = (UInt16)((UInt16)(arraydata_after_escape[arraydata_after_escape.Count - 3] << 8) | (UInt16)arraydata_after_escape[arraydata_after_escape.Count - 2]);
                    if (tmpcrc == receive_crc)
                    {                        
                        string savestr;
                        Array.Copy(senddata, senddata1, length - 2);
                        RemoteApiRs232.ApiMessage msg = RemoteApiRs232.ApiMessage.Parser.ParseFrom(senddata1);

                        save_log(msg.ApiMessageCase.ToString());
                        
                        switch (msg.ApiMessageCase)
                        {
                            case RemoteApiRs232.ApiMessage.ApiMessageOneofCase.StartProduct:
                                Console.WriteLine(1);
                                savestr = RemoteApiRs232.ApiMessage.ApiMessageOneofCase.StartProduct.ToString();
                                
                                save_log(":1： " + savestr);

                                break;
                            case RemoteApiRs232.ApiMessage.ApiMessageOneofCase.GetProductList:
                                Console.WriteLine(2);
                                savestr = RemoteApiRs232.ApiMessage.ApiMessageOneofCase.GetProductList.ToString();
                                
                                save_log(":2： " + savestr);
                                break;
                            case RemoteApiRs232.ApiMessage.ApiMessageOneofCase.ProductStarted:
                                Console.WriteLine(3);
                                savestr = msg.ProductStarted.ResponseCode.GetHashCode().ToString();

                                save_log(":3： " + savestr);
                                Console.WriteLine(msg.ProductStarted.ResponseCode);
                                ResponseCode = msg.ProductStarted.ResponseCode.GetHashCode();
                                switch (msg.ProductStarted.ResponseCode)
                                {
                                    case RemoteApiRs232.ResponseCode.DbusAdapterError:
                                        Console.WriteLine(1);
                                        product_status = 3;

                                        break;
                                    case RemoteApiRs232.ResponseCode.GeneralError:
                                        Console.WriteLine(1);
                                        product_status = 3;

                                        break;
                                    case RemoteApiRs232.ResponseCode.InvalidParameter:
                                        Console.WriteLine(1);
                                        product_status = 3;

                                        break;
                                    case RemoteApiRs232.ResponseCode.ProductNotAvailable:
                                        Console.WriteLine(1);
                                        product_status = 3;

                                        break;
                                    case RemoteApiRs232.ResponseCode.Success:
                                        product_status = 0;
                                        Console.WriteLine(1);
                                        break;
                                    case RemoteApiRs232.ResponseCode.SystemBusy:
                                        Console.WriteLine(1);
                                        product_status = 3;
                                        savestr = msg.ProductStarted.ResponseCode.GetHashCode().ToString();
                                        save_log(":SystemBusy3： " + savestr);
                                       

                                        break;
                                    case RemoteApiRs232.ResponseCode.UnknownProductId:
                                        Console.WriteLine(1);
                                        product_status = 3;

                                        break;
                                    case RemoteApiRs232.ResponseCode.UnknownResponseCode:
                                        Console.WriteLine(1);
                                        product_status = 3;
                                        break;
                                }
                                break;
                            case RemoteApiRs232.ApiMessage.ApiMessageOneofCase.ProductList:
                                Console.WriteLine(4);
                                foreach (var product in msg.ProductList.ProductList_)
                                {
                                    id_list.Add(product.Key);
                                    name_list.Add(product.Value);
                                    savestr = product.Key + "--" + product.Value;
                                    save_log(":4： " + savestr);
                                }
                                productlistAvailable = true;
                                msg_list.Add("product list！");

                                savestr = msg.ProductList.ResponseCode.ToString();
                                save_log(":5： " + savestr);
                                
                                switch (msg.ProductList.ResponseCode)
                                {
                                    case RemoteApiRs232.ResponseCode.DbusAdapterError:
                                        Console.WriteLine(1);
                                        break;
                                    case RemoteApiRs232.ResponseCode.GeneralError:
                                        Console.WriteLine(1);
                                        break;
                                    case RemoteApiRs232.ResponseCode.InvalidParameter:
                                        Console.WriteLine(1);
                                        break;
                                    case RemoteApiRs232.ResponseCode.ProductNotAvailable:
                                        Console.WriteLine(1);
                                        break;
                                    case RemoteApiRs232.ResponseCode.Success:
                                        Console.WriteLine(1);
                                        break;
                                    case RemoteApiRs232.ResponseCode.SystemBusy:
                                        Console.WriteLine(1);
                                        break;
                                    case RemoteApiRs232.ResponseCode.UnknownProductId:
                                        Console.WriteLine(1);
                                        break;
                                    case RemoteApiRs232.ResponseCode.UnknownResponseCode:
                                        Console.WriteLine(1);
                                        break;
                                }
                                break;
                            case RemoteApiRs232.ApiMessage.ApiMessageOneofCase.ErrorEvent:
                                msg_code = msg.ErrorEvent.ErrorNumber;
                                savestr = msg_code.ToString();
                                save_log(":6： " + savestr);
                                switch (msg_code)
                                {
                                    case 0:
                                        break;
                                    case 1:
                                        coffee_machine_status_t.BoilerControllerOK = 0;
                                        break;
                                    case 2:
                                        coffee_machine_status_t.BoilerControllerOK = 2;
                                        break;
                                    case 3:
                                        coffee_machine_status_t.BoilerControllerOK = 0;
                                        break;
                                    case 4:
                                        coffee_machine_status_t.BoilerControllerOK = 4;
                                        break;
                                    case 5:
                                        coffee_machine_status_t.BoilerControllerOK = 5;
                                        break;
                                    case 6:
                                        coffee_machine_status_t.BoilerControllerOK = 6;
                                        break;
                                    case 7:
                                        coffee_machine_status_t.BoilerControllerOK = 0;
                                        break;
                                    case 8:
                                        coffee_machine_status_t.StatusManagerOK = 0;
                                        break;
                                    case 9:
                                        coffee_machine_status_t.StatusManagerOK = 9;
                                        break;
                                    case 10:
                                        coffee_machine_status_t.BeanHopperPresent = 10;
                                        break;
                                    case 11:
                                        coffee_machine_status_t.BeanHopperPresent = 11;
                                        break;
                                    case 12:
                                        coffee_machine_status_t.BeanHopperLevelOK = 0;
                                        break;
                                    case 13:
                                        coffee_machine_status_t.BeanHopperLevelOK = 13;
                                        break;
                                    case 14:
                                        coffee_machine_status_t.BeanHopperLevelOK = 14;
                                        break;
                                    case 15:
                                        //coffee_machine_status_t.GroundsDrawerOK = 0;
                                        break;
                                    case 16:
                                        coffee_machine_status_t.GroundsDrawerOK = 16;
                                        break;
                                    case 17:
                                        coffee_machine_status_t.GroundsDrawerOK = 0;
                                        break;
                                    case 18:
                                        coffee_machine_status_t.GroundsDrawerOK = 18;
                                        break;
                                    case 19:
                                        coffee_machine_status_t.GroundsDrawerOK = 19;
                                        break;
                                    case 20:
                                        coffee_machine_status_t.MilkTemperatureOK = 0;
                                        break;
                                    case 21:
                                        coffee_machine_status_t.MilkTemperatureOK = 21;
                                        break;
                                    case 22:
                                        coffee_machine_status_t.MilkTemperatureOK = 22;
                                        break;
                                    case 23:
                                        coffee_machine_status_t.MilkLevelOK = 0;
                                        break;
                                    case 24:
                                        coffee_machine_status_t.MilkLevelOK = 24;
                                        break;
                                    case 25:
                                        coffee_machine_status_t.MilkLevelOK = 25;
                                        break;
                                    case 26:
                                        coffee_machine_status_t.CleaningOK = 0;
                                        break;
                                    case 27:
                                        coffee_machine_status_t.CleaningOK = 27;
                                        break;
                                    case 28:
                                        coffee_machine_status_t.CleaningOK = 28;
                                        break;
                                    case 29:
                                        //coffee_machine_status_t. = 0;
                                        break;
                                    case 30:
                                        coffee_machine_status_t.PistonError = 1;
                                        break;
                                    case 31:
                                        coffee_machine_status_t.CommunicationError = 1;
                                        break;
                                    case 32:
                                        coffee_machine_status_t.ComponentError = 1;
                                        break;
                                    case 33:
                                        coffee_machine_status_t.ConfigurationError = 1;
                                        break;
                                    case 34:
                                        coffee_machine_status_t.FlowError = 1;
                                        break;
                                    case 35:
                                        coffee_machine_status_t.BrewChamberEmpty = 1;
                                        break;
                                    case 36:
                                        coffee_machine_status_t.BrewChamberTooFull = 1;
                                        break;
                                    case 37:
                                        coffee_machine_status_t.MilkEmptyDuringProduct = 1;
                                        break;
                                    case 38:
                                        coffee_machine_status_t.SafetyReedDuringProduct = 1;
                                        break;
                                    case 39:
                                        //coffee_machine_status_t.boiler = 0;
                                        break;
                                    case 40:
                                        break;
                                    case 41:
                                        //coffee_machine_status_t.boiler = 0;
                                        break;
                                    case 42:
                                        //coffee_machine_status_t.boiler = 0;
                                        break;
                                    case 43:
                                        coffee_machine_status_t.ChassisFanFailure = 0;
                                        break;
                                    case 44:
                                        coffee_machine_status_t.ChassisFanFailure = 1;
                                        break;
                                    case 45:
                                        //coffee_machine_status_t.boiler = 0;
                                        break;
                                    case 46:
                                        //coffee_machine_status_t.boiler = 0;
                                        break;
                                    case 47:
                                        //coffee_machine_status_t.boiler = 0;
                                        break;
                                    case 48:
                                        //coffee_machine_status_t.boiler = 0;
                                        break;
                                    case 49:
                                        //coffee_machine_status_t.boiler = 0;
                                        break;
                                    default: break;

                                }


                                break;
                            case RemoteApiRs232.ApiMessage.ApiMessageOneofCase.ProductFinished:
                                Console.WriteLine(7);
                                
                                if (msg.ProductFinished.Success == true && msg.ProductFinished.ProductId == localproductid)
                                {
                                    product_status = 1;
                                }
                                else if (msg.ProductFinished.Success == false && msg.ProductFinished.ProductId == localproductid)
                                {
                                    product_status = 2;
                                }
                                try
                                {
                                    savestr = msg.ProductFinished.ProductId.ToString() + "--" + msg.ProductFinished.Success + ";product_status--" + product_status;

                                    save_log(":7： " + savestr);
                                }
                                catch
                                {

                                }
                                break;
                            case RemoteApiRs232.ApiMessage.ApiMessageOneofCase.ProductAvailabilityChanged:
                                Console.WriteLine(8);
                                savestr = RemoteApiRs232.ApiMessage.ApiMessageOneofCase.ProductAvailabilityChanged.ToString();
                                save_log(":8： " + savestr);
                                break;
                            case RemoteApiRs232.ApiMessage.ApiMessageOneofCase.RinsingUpcoming:
                                Console.WriteLine(9);
                                savestr = RemoteApiRs232.ApiMessage.ApiMessageOneofCase.RinsingUpcoming.ToString();

                                save_log(":9： " + savestr);
                                break;
                            case RemoteApiRs232.ApiMessage.ApiMessageOneofCase.UnknownMessage:
                                Console.WriteLine(10);
                                savestr = RemoteApiRs232.ApiMessage.ApiMessageOneofCase.UnknownMessage.ToString();

                                save_log(":10： " + savestr);
                                break;
                            case RemoteApiRs232.ApiMessage.ApiMessageOneofCase.WrongCrc:
                                Console.WriteLine(11);
                                savestr = RemoteApiRs232.ApiMessage.ApiMessageOneofCase.WrongCrc.ToString();
                                save_log(":11： " + savestr);
                                break;
                            case RemoteApiRs232.ApiMessage.ApiMessageOneofCase.BrokenMessage:
                                Console.WriteLine(12);
                                savestr = RemoteApiRs232.ApiMessage.ApiMessageOneofCase.BrokenMessage.ToString();

                                save_log(":12： " + savestr);
                                break;
                        }

                    }
                    arraydata.RemoveRange(0, end_position + 1); ;
                    arraydata_after_escape.Clear();
                    dealdata.Clear();
                    start_flag = false;
                    end_flag = false;
                }
            }
            try
            {
                arraydata.RemoveRange(0, start_position); //删除无用数据
            }
            catch
            {

            }
        }


        public void init_serial(string port, int baundrate)
        {
            try
            {
                serial_status = true;
                if (m_serial_port.IsOpen)
                {
                    m_serial_port.Close();
                }
                m_serial_port_number = port;
                m_serial_port_baud_rate = baundrate;

                m_serial_port.PortName = m_serial_port_number;
                m_serial_port.BaudRate = m_serial_port_baud_rate;
                m_serial_port.DataBits = 8;
                m_serial_port.StopBits = StopBits.One;
                m_serial_port.Parity = Parity.None;
                m_serial_port.ReadTimeout = -1;
                m_serial_port.WriteTimeout = -1;
                m_serial_port.WriteBufferSize = 512;
                m_serial_port.ReadBufferSize = 512;
                m_serial_port.ReceivedBytesThreshold = 1; //add at 2019 10-22 17:14
                m_serial_port.DataReceived += new SerialDataReceivedEventHandler(SerialDataReceivedHandler);
                m_serial_port.Open();
            }
            catch (Exception ex)
            {
                Console.WriteLine("serial init failed !");
                serial_status = false;
            }
        }

        public void close_serial()
        {
            try
            {
                if (m_serial_port.IsOpen)
                {
                    m_serial_port.Close();
                }
            }
            catch { }

        }

        public void start_product(string idStr)
        {
            //product_status = 0;
            
            msg_list.Clear();
            send_data_array.Clear();
            if (m_serial_port.IsOpen)
            {
                //02 8A 80 10 22 00 1D 38 03 

                //string sendid = id_list[index].ToString();
                string sendid = idStr;
                localproductid = sendid;
                
                save_log(":start：" + idStr + ";product_status=" + product_status);
                byte[] array = Encoding.ASCII.GetBytes(sendid);

                byte sendheader = 0x02;

                int length = array.Length + 8;
                byte[] senddata = new byte[length];
                senddata[0] = 0x8A;
                senddata[1] = 0x80;
                senddata[2] = 0x02;

                senddata[3] = (byte)(array.Length + 2);
                senddata[4] = 0x0A;
                senddata[5] = (byte)array.Length;
                Array.Copy(array, 0, senddata, 6, array.Length);
                append_crc16(senddata, length);
                //添加转义字符,获取需要转义的字符数量
                int zhuanyi_count = 0;
                for (int i = 0; i < length; i++)
                {
                    switch (senddata[i])
                    {
                        case 0x02:
                            zhuanyi_count++;
                            break;
                        case 0x03:
                            zhuanyi_count++;
                            break;
                        case 0x10:
                            zhuanyi_count++;
                            break;
                        default: break;
                    }
                }
                byte sendtailer = 0x03;
                int send_length = 2 + zhuanyi_count + length;//2代表首尾长度，zhuanyi_count 是转义需要添加的长度，length是除首尾的数据长度
                byte[] send_data = new byte[send_length];
                send_data_array.Add(sendheader);
                for (int k = 0; k < length; k++)
                {
                    switch (senddata[k])
                    {
                        case 0x02:
                            send_data_array.Add(0x10);
                            send_data_array.Add(0x22);
                            break;
                        case 0x03:
                            send_data_array.Add(0x10);
                            send_data_array.Add(0x23);
                            break;
                        case 0x10:
                            send_data_array.Add(0x10);
                            send_data_array.Add(0x30);
                            break;
                        default:
                            send_data_array.Add(senddata[k]);
                            break;
                    }
                }
                send_data_array.Add(sendtailer);
                for (int k = 0; k < send_length; k++)
                {
                    send_data[k] = send_data_array[k];
                }
                //发送字符串
                try
                {
                    
                    m_serial_port.Write(send_data, 0, send_length);
                    //product_status = 0;
                }
                catch
                {
                    serial_status = false;
                }


            }
            else
            {
                serial_status = false;
            }
        }


        public void get_product_list()
        {
            id_list.Clear();
            name_list.Clear();
            productlistAvailable = false;
            if (m_serial_port.IsOpen)
            {
                //02 92 80 10 22 00 B3 90 03
                byte[] senddata = new byte[9];
                senddata[0] = 0x02;
                senddata[1] = 0x92;
                senddata[2] = 0x80;
                senddata[3] = 0x10;
                senddata[4] = 0x22;
                senddata[5] = 0x00;
                senddata[6] = 0xB3;
                senddata[7] = 0x90;
                senddata[8] = 0x03;

                m_serial_port.Write(senddata, 0, 9);
            }
        }
    }
}
