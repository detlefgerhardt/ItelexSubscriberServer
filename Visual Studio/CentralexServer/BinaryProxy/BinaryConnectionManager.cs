using ServerCommon;
using System.Net.Sockets;
using System.Net;
using CentralexServer;
using ServerCommon.Logging;

namespace Centralex.BinaryProxy
{
	internal enum DisconnectReasons { NotConnected, TcpDisconnect, TcpDisconnectByRemote, SendCmdError };

	internal class BinaryConnectionManager
	{
		private const string TAG = nameof(BinaryConnectionManager);
		private const string TAG2 = "";

		protected Logger _logger;

		private TcpListener _tcpListener;

		private bool _shutDown;

		private static BinaryConnectionManager instance;
		public static BinaryConnectionManager Instance => instance ??= new BinaryConnectionManager();

		private BinaryConnectionManager()
		{
			_logger = GlobalData.Logger;
		}

		public bool SetRecvOn()
		{
			if (GlobalData.Config.TlnServerProxyPort == null) return true; // do not use proxy

			_logger.Debug(TAG, nameof(SetRecvOn), TAG2, "start tln server proxy");

			try
			{
				_tcpListener = new TcpListener(IPAddress.Any, GlobalData.Config.TlnServerProxyPort.Value);
				_tcpListener.Start();

				// start listener task for incoming connections
				Task _listenerTask = Task.Run(() => Listener());
				return true;
			}
			catch (Exception ex)
			{
				_logger.Error(TAG, nameof(SetRecvOn), TAG2, "", ex);
				return false;
			}
		}

		public bool SetRecvOff()
		{
			_logger.Debug(TAG, nameof(SetRecvOff), TAG2, "");
			_tcpListener.Stop();
			return true;
		}

		private void Listener()
		{
			TaskMonitor.Instance.AddTask(Task.CurrentId, TAG, nameof(Listener));

			_logger.Info(TAG, nameof(Listener), TAG2, 
					$"waiting for binary connections at port {GlobalData.Config.TlnServerProxyPort}");

			while (true)
			{
				if (_shutDown)
				{
					SetRecvOff();
					TaskMonitor.Instance.RemoveTask(Task.CurrentId);
					return;
				}

				try
				{
					_logger.Debug(TAG, nameof(Listener), TAG2, 
							$"Wait for connection port {GlobalData.Config.TlnServerProxyPort}");

					// wait for connection
					//TickTimer pendingTimer = new TickTimer();
					//while (!_tcpListener.Pending())
					//{
					//	Thread.Sleep(50);
					//}
					TcpClient tcpClient = _tcpListener.AcceptTcpClient();

					_logger.ConsoleLog(TAG, nameof(Listener), TAG2, 
							$"incoming proxy connection {GlobalData.Config.TlnServerProxyPort}");

					Task.Run(() =>
					{
						TaskMonitor.Instance.AddTask(Task.CurrentId, TAG, nameof(Listener));
						try
						{
							BinaryIncommingConnection conn = new BinaryIncommingConnection(tcpClient,
								Constants.LOG_PATH, GlobalData.Config.LogLevel);
							conn.Start();
							conn.Dispose();
						}
						finally
						{
						}
						TaskMonitor.Instance.RemoveTask(Task.CurrentId);
					});
				}
				catch (Exception ex)
				{
					if (ex.HResult == -2147467259)
					{
						_logger.Error(TAG, nameof(Listener), TAG2, $"Listener ex={ex.Message}");
					}
					else
					{
						_logger.Error(TAG, nameof(Listener), TAG2, "", ex);
					}
				}
			}
		}

		public virtual void Shutdown()
		{
			_shutDown = true;
		}
	}
}
