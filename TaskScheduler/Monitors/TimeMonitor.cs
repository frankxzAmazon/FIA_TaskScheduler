using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace TaskScheduler.Monitors
{
    public class TimeMonitor:IMonitor
    {
        private static readonly ILog Logger = LogManager.GetLogger(Environment.MachineName);
        private ElapsedEventHandler _onTimedEvent;
        private Timer _timer = new Timer()
        {
            AutoReset = false,
            Enabled = false
            
        };
        private int countDownDay = 0;
        private int timerCount = 0;
        public TimeMonitor()
        {
            _onTimedEvent = new ElapsedEventHandler(OnTimedEvent);
        }

        public DateTime NextTime { get; set; }
        public int Frequency { get; set; }
        public  DateTimeUnit Unit{ get; set; }
        public string MonitorName { get; set; }
        public IJob Job { get; set; }
        public void BeginMonitoring()
        {
            Job.IsOn = true;
            StartTimer();
            Logger.Info("Started timer " + MonitorName);
            Job.UpdateStatus();
        }
        public DateTime UpdateNextTime()
        {
            DateTime nextTime = NextTime;
            while (nextTime < DateTime.UtcNow)
            {
                nextTime = AddTime(nextTime);
            }
            return nextTime;
        }
        public DateTime AddTime(DateTime nextTime)
        {
            switch (Unit)
            {
                case DateTimeUnit.Minutes:
                    return nextTime.AddMinutes(Frequency);
                case DateTimeUnit.Hours:
                    return nextTime.AddHours(Frequency);
                case DateTimeUnit.Days:
                    return nextTime.AddDays(Frequency);
                case DateTimeUnit.Weeks:
                    return nextTime.AddDays(Frequency * 7);
                case DateTimeUnit.Months:
                    return nextTime.AddMonths(Frequency);
                case DateTimeUnit.Years:
                    return nextTime.AddYears(Frequency);
                default: throw new NotImplementedException(Unit.ToString() + " not implemented.");
            }
        }
        public void EndMonitoring()
        {
            Logger.Info("Ended Timer " + MonitorName);
            _timer.Close();
        }

        public bool Compare(IMonitor monitor)
        {
            var other = monitor as TimeMonitor;
            return other != null &&
                   UpdateNextTime() == other.UpdateNextTime() &&
                   Frequency == other.Frequency &&
                   Unit == other.Unit &&
                   MonitorName == other.MonitorName &&
                   Job.Compare(other.Job);
        }

        private void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            if(countDownDay == 0)
            {
                Job.StartJob();
                timerCount--;
                _timer.Elapsed -= _onTimedEvent;
                StartTimer();
            }
            else
            {
                countDownDay -= 1;
                timerCount--;
                StartTimer();
            }
        }
        private void StartTimer()
        {
            if (timerCount == 0)
            {
                NextTime = UpdateNextTime();
                countDownDay = Math.Max((int)(NextTime - DateTime.UtcNow).TotalDays - 1, 0);
                if (countDownDay > 1)
                {
                    _timer.Interval = 86400000; //milliseconds in a day
                }
                else
                {
                    _timer.Interval = Math.Max(10000, (NextTime - DateTime.UtcNow).TotalMilliseconds);
                }

                _timer.Elapsed += _onTimedEvent;
                timerCount++;
                _timer.Start();
            }
        }
    }
}
