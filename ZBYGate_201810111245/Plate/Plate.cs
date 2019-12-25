﻿using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using ZBYGate_Data_Collection.Log;

namespace ZBYGate_Data_Collection.Plate
{
    class Plate:IDisposable
    {
        private CLog Log = new CLog();

        public Action<string> SetMessageAction;//日志信息
        public Action<string, uint> PlateCallBackAction;//通讯状态
        public Action<string, string, string, DateTime> PlateDataCallBackAction;//识别结果
        public Action<byte[]> JpegCallBackAction;//图片流回调
        public Action<byte[]> DataJpegCallBackAction;//识别结果图片回调

        /// <summary>
        /// 回调函数: 通知相机设备通讯状态的回调函数	
        /// </summary>
        public CLIENT_LPRC_ConnectCallback ConnectCallback = null;

        /// <summary>
        /// 回调函数: 获取识别结果的回调函数	
        /// </summary>
        public CLIENT_LPRC_DataEx2Callback DataEx2Callback = null;

        /// <summary>
        /// 回调函数: 获取Jpeg流的回调函数
        /// </summary>
        public CLIENT_LPRC_JpegCallback JpegCallback = null;

        /// <summary>
        /// 回调函数:获取相机485发送的数据	
        /// </summary>
        //public CLIENT_LPRC_SerialDataCallback SerialDataCallback = null;

        /// <summary>
        /// Jpeg流回调返回每一帧jpeg数据结构体
        /// </summary>
        private CLIENT_LPRC_DEVDATA_INFO JpegInfo;

        /// <summary>
        /// 设备信息
        /// </summary>
        private CLIENT_LPRC_DeviceInfo DeviceInfo;

        /// <summary>
        /// 识别结果
        /// </summary>
        private static CLIENT_LPRC_PLATE_RESULTEX recRes;

        /// <summary>
        /// IP
        /// </summary>
        private IntPtr pIP = IntPtr.Zero;

        /// <summary>
        /// 运行状态
        /// </summary>
        private bool Running = false;

        /// <summary>
        /// 查找设备
        /// </summary>
        private bool SearchDevice = false;

        /// <summary>
        /// 图片流大小
        /// </summary>
        //private byte[] chJpegStream = new byte[NativeConstants.CLIENT_LPRC_BIG_PICSTREAM_SIZE_EX + 312];

        //开机链接
        private System.Threading.Timer CallbackTimer;


        public Plate()
        {
            ConnectCallback = new CLIENT_LPRC_ConnectCallback(OnConnectCallback);
            DataEx2Callback = new CLIENT_LPRC_DataEx2Callback(OnDataEx2Callback);
            JpegCallback = new CLIENT_LPRC_JpegCallback(OnJpegCallback);
            //SerialDataCallback = new CLIENT_LPRC_SerialDataCallback(OnSerialDataCallback);
            pIP = Marshal.StringToHGlobalAnsi(Properties.Settings.Default.Plate_IPAddr);

            //注册回调函数
            NativeMethods.CLIENT_LPRC_RegCLIENTConnEvent(ConnectCallback);
            NativeMethods.CLIENT_LPRC_RegDataEx2Event(DataEx2Callback);
            NativeMethods.CLIENT_LPRC_RegJpegEvent(JpegCallback);
            //NativeMethods.CLIENT_LPRC_RegSerialDataEvent(SerialDataCallback);

            CallbackTimer = new System.Threading.Timer(PlateStartLink, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(0));

        }

        /// <summary>
        /// 自动链接车牌
        /// </summary>
        /// <param name="state"></param>
        private void PlateStartLink(object state)
        {
            CallbackFuntion(1);
        }

        /// <summary>
        /// 获取相机485发送的数据
        /// </summary>
        /// <param name="chCLIENTIP"></param>
        /// <param name="pSerialData"></param>
        /// <param name="dwUser"></param>
        //private void OnSerialDataCallback(IntPtr chCLIENTIP, ref CLIENT_LPRC_DEVSERIAL_DATA pSerialData, uint dwUser)
        //{
        //    //throw new NotImplementedException();
        //}

        /// <summary>
        /// 视频显示回调
        /// </summary>
        /// <param name="JpegInfo"></param>
        /// <param name="dwUser"></param>
        private void OnJpegCallback(ref CLIENT_LPRC_DEVDATA_INFO JpegInfo, uint dwUser)
        {
            if (Running == true)
            {
                this.JpegInfo = JpegInfo;
                //把图像数据拷贝到指定内存
                byte[] chJpegStream = new byte[NativeConstants.CLIENT_LPRC_BIG_PICSTREAM_SIZE_EX + 312];
                uint nJpegStream = this.JpegInfo.nLen;
                Array.Clear(chJpegStream, 0, chJpegStream.Length);
                if (this.JpegInfo.pchBuf == IntPtr.Zero)
                {
                    return;
                }
                Marshal.Copy(this.JpegInfo.pchBuf, chJpegStream, 0, (Int32)nJpegStream);
                JpegCallBackAction?.Invoke(chJpegStream);
                //Array.Clear(chJpegStream, 0, chJpegStream.Length);
               
            }
        }

        /// <summary>
        /// 识别结果回调
        /// </summary>
        /// <param name="recResultEx"></param>
        /// <param name="dwUser"></param>
        private void OnDataEx2Callback(ref CLIENT_LPRC_PLATE_RESULTEX recResultEx, uint dwUser)
        {
            recRes = recResultEx;
            DateTime datetime = new DateTime(recRes.shootTime.Year, recRes.shootTime.Month, recRes.shootTime.Day, recRes.shootTime.Hour, recRes.shootTime.Minute, recRes.shootTime.Second);
            string time = datetime.ToString("yyyy-MM-dd HH:mm:ss");
            PlateDataCallBackAction?.Invoke(recRes.chCLIENTIP, recRes.chLicense, recRes.chColor, datetime);
            SetMessageAction?.Invoke(string.Format("Plate Result Time：{0} Plate：{1}", time, recRes.chLicense));
            Log.logInfo.Info(string.Format("Plate Result Time：{0} Plate：{1}", time, recRes.chLicense));
            JpegData(recRes);
        }

        /// <summary>
        /// 显示识别结果中的图片
        /// </summary>
        /// <param name="recResultEx"></param>
        private void JpegData(CLIENT_LPRC_PLATE_RESULTEX recResultEx)
        {
            Int32 nJpegStream = recResultEx.pFullImage.nLen;
            byte[] chJpegStream = new byte[NativeConstants.CLIENT_LPRC_BIG_PICSTREAM_SIZE_EX + 312];
            Array.Clear(chJpegStream, 0, chJpegStream.Length);
            Marshal.Copy(recResultEx.pFullImage.pBuffer, chJpegStream, 0, nJpegStream);
            DataJpegCallBackAction?.Invoke(chJpegStream);
            //Array.Clear(chJpegStream, 0, chJpegStream.Length);
        }

        /// <summary>
        /// 通知设备通讯状态
        /// </summary>
        /// <param name="chCLIENTIP">IP</param>
        /// <param name="nStatus">状态</param>
        /// <param name="dwUser"></param>
        private void OnConnectCallback(IntPtr chCLIENTIP, uint nStatus, uint dwUser)
        {
            PlateCallBackAction?.Invoke(chCLIENTIP.ToString(), nStatus);
        }

        /// <summary>
        /// 开始链接
        /// </summary>
        public void CallbackFuntion(int i)
        {
            if (NativeMethods.CLIENT_LPRC_InitSDK(Properties.Settings.Default.Plate_Port, IntPtr.Zero, 0, pIP, 1) != 0)
            {
                SetMessageAction?.Invoke(string.Format("{0} 初始化失败！", Marshal.PtrToStringAnsi(pIP)));
                Log.logWarn.Warn(string.Format("{0} 初始化失败！", Marshal.PtrToStringAnsi(pIP)));
                Running = false;
                CallbackTimer.Change(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(0));
            }
            else
            {
                SetMessageAction?.Invoke(string.Format("{0} 初始化成功！", Marshal.PtrToStringAnsi(pIP)));
                Log.logInfo.Info(string.Format("{0} 初始化成功！", Marshal.PtrToStringAnsi(pIP)));
                Running = true;
                CallbackTimer.Change(-1, -1);
            }
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        public void QuitDevice(int i)
        {
            if (Running == true)
            {
                Running = false;
                NativeMethods.CLIENT_LPRC_SetJpegStreamPlayOrStop(pIP, 0);
                if (NativeMethods.CLIENT_LPRC_QuitDevice(this.pIP) == 0)
                {
                    SetMessageAction?.Invoke(string.Format("{0} 设备断开", Marshal.PtrToStringAnsi(pIP)));
                    Log.logInfo.Info(string.Format("{0} 设备断开", Marshal.PtrToStringAnsi(pIP)));
                }
            }
            else
            {              
                CallbackTimer.Change(-1, -1);
                SetMessageAction?.Invoke(string.Format("{0} 关闭链接", Marshal.PtrToStringAnsi(pIP)));
            }
        }

        /// <summary>
        /// 模拟触发命令
        /// </summary>
        public void SetTrigger(int i)
        {
            if (Running == true)
            {
                if (NativeMethods.CLIENT_LPRC_SetTrigger(pIP, 8080) == 0)
                {
                    SetMessageAction?.Invoke(string.Format("{0} 模拟触发命令", Marshal.PtrToStringAnsi(pIP)));
                }
            }
        }

        /// <summary>
        /// 发送抬杆命令
        /// </summary>
        public void SetRelayClose(int i)
        {
            if (Running == true)
            {
                if (NativeMethods.CLIENT_LPRC_SetRelayClose(this.pIP, 9110) == 0)
                {
                    SetMessageAction?.Invoke(string.Format("{0} 发送抬杆命令", Marshal.PtrToStringAnsi(pIP)));
                }
            }
        }

        /// <summary>
        /// 发送485透明传输
        /// </summary>
        public void RS485Send(string mes)
        {
            byte[] prefix = HexToBytes(Properties.Settings.Default.Plate_LED_Prefix);
            byte[] data = Encoding.GetEncoding("GB2312").GetBytes(mes);
            byte[] end = new byte[] { 0x0D };
            byte[] dst = new byte[prefix.Length + data.Length + end.Length];
            prefix.CopyTo(dst, 0);
            data.CopyTo(dst, prefix.Length);
            end.CopyTo(dst, prefix.Length + data.Length);
            string x = Encoding.GetEncoding("GB2312").GetString(dst);//.Replace(" ", "\0");
            if (Running == true)
            {
                if (NativeMethods.CLIENT_LPRC_RS485Send(pIP, 9110, Marshal.StringToHGlobalAnsi(x), dst.Length) == 0)
                {
                    string tmp = string.Format("{0} 传输成功", Encoding.GetEncoding("GB2312").GetString(data));
                    SetMessageAction?.Invoke(tmp);
                    Log.logInfo.Info(tmp);
                }
            }
        }

        /// <summary>
        /// 字符转16进制
        /// </summary>
        /// <param name="hex"></param>
        /// <returns></returns>
        public static byte[] HexToBytes(string hex)
        {
            hex = hex.Trim();
            byte[] bytes = new byte[hex.Length / 2];
            for (int index = 0; index < bytes.Length; index++)
            {
                bytes[index] = byte.Parse(hex.Substring(index * 2, 2), NumberStyles.HexNumber);
            }
            return bytes;
        }

        /// <summary>
        /// 搜索设备
        /// </summary>
        public void SearchDeviceList(int i)
        {
            if (NativeMethods.CLIENT_LPRC_SearchDeviceList(ref DeviceInfo) > 0)
            {
                SetMessageAction?.Invoke(string.Format("搜索到设备：{0}", DeviceInfo.chIp.ToString()));
                SearchDevice = true;
            }
            else
            {
                SetMessageAction?.Invoke("Not Find Device");
            }
        }

        /// <summary>
        /// 设置网卡IP
        /// </summary>
        /// <param name="Ip"></param>
        public void SetIpNetwork(int i)
        {
            if (NativeMethods.CLIENT_LPRC_SetNetworkCardBind(Marshal.StringToHGlobalAnsi(Properties.Settings.Default.Plate_Local_IpAddr)) == 0)
            {
                SetMessageAction?.Invoke(string.Format("设置IP：{0}", Marshal.PtrToStringAnsi(pIP)));
            }
        }

        /// <summary>
        /// 设置保存路径
        /// </summary>
        /// <param name="path"></param>
        public void SetSaveImagePath(int i)
        {
            NativeMethods.CLIENT_LPRC_SetSavePath(Marshal.StringToHGlobalAnsi(Properties.Settings.Default.Plate_Image_Path));
            SetMessageAction?.Invoke(string.Format("设置图片保存路径：{0}",Properties.Settings.Default.Plate_Image_Path));
        }

        /// <summary>
        /// 设置设备地址，网关，掩码
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="gateway"></param>
        /// <param name="netmask"></param>
        public void SetDeviceInfoIp(string  ip,string gateway,string netmask)
        {
            DeviceInfo.chIp = ip;
            DeviceInfo.chGateway = gateway;
            DeviceInfo.chNetmask = netmask;
            if(SearchDevice)
            {
                if (NativeMethods.CLIENT_LPRC_AlterDeviceInfo(pIP, DeviceInfo) == 0)
                {
                    SetMessageAction?.Invoke(string.Format("更改设备信息成功：{0}|{1}|{2}", ip, gateway, netmask));
                }
            }
            SearchDevice = false;
        }

        /// <summary>
        /// 视频流
        /// </summary>
        public void Play(bool state)
        {
            if(Running)
            {
                if (state)
                {
                    NativeMethods.CLIENT_LPRC_RegJpegEvent(JpegCallback);
                    if (NativeMethods.CLIENT_LPRC_SetJpegStreamPlayOrStop(pIP, 1) == 0)
                    {
                        SetMessageAction?.Invoke("Open Plate Video！");
                    }
                }
                else
                {
                    NativeMethods.CLIENT_LPRC_RegJpegEvent(null);
                    if (NativeMethods.CLIENT_LPRC_SetJpegStreamPlayOrStop(pIP, 0) == 0)
                    {
                        SetMessageAction?.Invoke("Close Plate Video！");
                    }
                    //Array.Clear(chJpegStream, 0, chJpegStream.Length);
                }
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // 要检测冗余调用

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: 释放托管状态(托管对象)。
                    CallbackTimer.Dispose();                   
                }
                //释放所有SDK资源
                Play(false);
                ConnectCallback = null;
                DataEx2Callback = null;
                JpegCallback = null;
                //SerialDataCallback = null;
                NativeMethods.CLIENT_LPRC_QuitSDK();

                // TODO: 释放未托管的资源(未托管的对象)并在以下内容中替代终结器。
                // TODO: 将大型字段设置为 null。
                disposedValue = true;
            }
        }

        // TODO: 仅当以上 Dispose(bool disposing) 拥有用于释放未托管资源的代码时才替代终结器。
        ~Plate()
        {
            // 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
            Dispose(false);
        }

        // 添加此代码以正确实现可处置模式。
        public void Dispose()
        {
            // 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
            Dispose(true);
            // TODO: 如果在以上内容中替代了终结器，则取消注释以下行。
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
