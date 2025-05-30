namespace ServerCommon.Utility
{
	public class TickTimer
	{
		private long _msTicks;

		public bool IsStarted { get; private set; }

		public TickTimer()
		{
			Start();
		}

		public TickTimer(bool start)
		{
			IsStarted = start;
			if (start)
			{
				Start();
			}
		}
		public void Start()
		{
			_msTicks = MilliTicks();
			IsStarted = true;
		}

		public void Stop()
		{
			_msTicks = 0;
			IsStarted = false;
		}

		private long MilliTicks()
		{
			return DateTime.Now.Ticks / 10000;
		}

		public bool IsElapsedMilliseconds(int milliseconds)
		{
			if (!IsStarted) return false;
			return ElapsedMilliseconds > milliseconds;
		}

		public bool IsElapsedSeconds(int seconds)
		{
			return IsElapsedMilliseconds(seconds * 1000);
		}

		public bool IsElapsedMinutes(int minutes)
		{
			return IsElapsedMilliseconds(minutes * 1000 * 60);
		}

		public bool IsElapsedHours(int hours)
		{
			return IsElapsedMilliseconds(hours * 1000 * 60 * 60);
		}

		public int ElapsedSeconds
		{
			get
			{
				return (int)(ElapsedMilliseconds / 1000);
			}
		}

		public long ElapsedMilliseconds
		{
			get
			{
				if (!IsStarted) return 0;
				return MilliTicks() - _msTicks;
			}
		}

		public long Milliseconds
		{
			get
			{
				return _msTicks;
			}
		}

		/// <summary>
		/// get systems ticks in ms
		/// </summary>
		public static long GetTicksMs()
		{
			return DateTime.Now.Ticks / 10000;
		}
	}
}
