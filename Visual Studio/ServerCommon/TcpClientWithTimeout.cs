﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ServerCommon
{
	public class TcpClientWithTimeout
	{
		protected string _hostname;
		protected int _port;
		protected int _timeout_milliseconds;
		protected TcpClient connection;
		protected bool connected;
		protected Exception exception;

		public TcpClientWithTimeout(string hostname, int port, int timeout_milliseconds)
		{
			_hostname = hostname;
			_port = port;
			_timeout_milliseconds = timeout_milliseconds;
		}

		public TcpClient Connect()
		{
			// kick off the thread that tries to connect
			connected = false;
			exception = null;
			Thread thread = new Thread(new ThreadStart(BeginConnect));
			thread.IsBackground = true; // So that a failed connection attempt 
										// wont prevent the process from terminating while it does the long timeout
			thread.Start();

			// wait for either the timeout or the thread to finish
			thread.Join(_timeout_milliseconds);

			if (connected)
			{
				// it succeeded, so return the connection
				thread.Abort();
				return connection;
			}
			if (exception != null)
			{
				// it crashed, so return the exception to the caller
				thread.Abort();
				throw exception;
			}
			else
			{
				// if it gets here, it timed out, so abort the thread and throw an exception
				thread.Abort();
				string message = string.Format("TcpClient connection to {0}:{1} timed out", _hostname, _port);
				throw new TimeoutException(message);
			}
		}

		protected void BeginConnect()
		{
			try
			{
				connection = new TcpClient(_hostname, _port);
				// record that it succeeded, for the main thread to return to the caller
				connected = true;
			}
			catch (Exception ex)
			{
				// record the exception for the main thread to re-throw back to the calling code
				exception = ex;
			}
		}
	}
}
