using System.Diagnostics;

namespace ServerCommon.Utility
{
	public static class Processes
	{
		public static List<ProcessItem> GetThreadlist(string procName = null)
		{
			List<ProcessItem> procItems = new List<ProcessItem>();

			if (string.IsNullOrEmpty(procName))
			{
				Process proc = Process.GetCurrentProcess();
				procName = proc.ProcessName;
			}
			Process[] pp = Process.GetProcessesByName(procName);

			foreach (Process p in pp)
			{
				foreach (ProcessThread t in p.Threads)
				{
#pragma warning disable CA1416 // Validate platform compatibility
					ProcessItem procItem = new ProcessItem()
					{
						Id = t.Id,
						Prio = t.CurrentPriority,
						PrioLevel = t.PriorityLevel,
						State = t.ThreadState,
						WaitReason = t.ThreadState == System.Diagnostics.ThreadState.Wait ? t.WaitReason : ThreadWaitReason.Unknown,
						StartTime = t.StartTime,
						TotalProcessorTime = t.TotalProcessorTime
					};
#pragma warning restore CA1416 // Validate platform compatibility
					procItems.Add(procItem);
				}
			}
			return procItems;
		}
	}

	public class ProcessItem
	{
		public int Id { get; set; }

		public int Prio { get; set; }

		public ThreadPriorityLevel PrioLevel { get; set; }

		public System.Diagnostics.ThreadState State { get; set; }

		public ThreadWaitReason WaitReason { get; set; }

		public DateTime StartTime { get; set; }

		public TimeSpan TotalProcessorTime { get; set; }

		public override string ToString()
		{
			return $"{Id} {PrioLevel} {Prio} {State} {WaitReason} {StartTime:HH.mm.ss dd.MM.yy}";
		}
	}
}

