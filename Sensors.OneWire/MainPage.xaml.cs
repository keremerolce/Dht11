using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Sensors.Dht;
using Sensors.OneWire.Common;
using Windows.Devices.Gpio;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Navigation;
using SQLite.Net;
using LightBuzz.SMTP;
using Windows.ApplicationModel.Email;
using Windows.UI.Popups;

namespace Sensors.OneWire
{
    public sealed partial class MainPage : BindablePage
    {
        int sayac;
        string path;
        SQLiteConnection conm;
        string a = "SELECT * FROM TableName";
        private DispatcherTimer _timer = new DispatcherTimer();

        GpioPin _pin = null;
        private IDht _dht = null;
        private List<int> _retryCount = new List<int>();
        private DateTimeOffset _startedAt = DateTimeOffset.MinValue;
        public MainPage()
        {

            this.InitializeComponent();
            path = Path.Combine(Windows.Storage.ApplicationData.Current.LocalCacheFolder.Path, "Uzem.sqlite");
            //path = Path.Combine(Windows.Storage.ApplicationData.Current.LocalCacheFolder.Path, "Uzem.sqlite");
            conm = new SQLite.Net.SQLiteConnection(new SQLite.Net.Platform.WinRT.SQLitePlatformWinRT(), path);
            conm.CreateTable<Db>();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += this.Timer_Tick;

        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            GpioController controller = GpioController.GetDefault();

            if (controller != null)
            {
                _pin = GpioController.GetDefault().OpenPin(2, GpioSharingMode.Exclusive);
                _dht = new Dht11(_pin, GpioPinDriveMode.Input);
                _timer.Start();
                _startedAt = DateTimeOffset.Now;

                // ***
                // *** Uncomment to simulate heavy CPU usage
                // ***
                //CpuKiller.StartEmulation();
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            _timer.Stop();

            // ***
            // *** Dispose the pin.
            // ***
            if (_pin != null)
            {
                _pin.Dispose();
                _pin = null;
            }

            // ***
            // *** Set the Dht object reference to null.
            // ***
            _dht = null;

            // ***
            // *** Stop the high CPU usage simulation.
            // ***
            CpuKiller.StopEmulation();

            base.OnNavigatedFrom(e);
        }

        private async void Timer_Tick(object sender, object e)
        {
            DhtReading reading = new DhtReading();
            int val = this.TotalAttempts;
            this.TotalAttempts++;

            reading = await _dht.GetReadingAsync().AsTask();

            _retryCount.Add(reading.RetryCount);
            this.OnPropertyChanged(nameof(this.AverageRetriesDisplay));
            this.OnPropertyChanged(nameof(this.TotalAttempts));
            this.OnPropertyChanged(nameof(this.PercentSuccess));

            if (reading.IsValid)
            {
                this.TotalSuccess++;
                this.Temperature = Convert.ToSingle(reading.Temperature);
                this.Humidity = Convert.ToSingle(reading.Humidity);
                this.LastUpdated = DateTimeOffset.Now;
                this.OnPropertyChanged(nameof(this.SuccessRate));
            }

            this.OnPropertyChanged(nameof(this.LastUpdatedDisplay));



            Task.Delay(2000).Wait();
        }

        public string PercentSuccess
        {
            get
            {
                string returnValue = string.Empty;

                int attempts = this.TotalAttempts;

                if (attempts > 0)
                {
                    returnValue = string.Format("{0:0.0}%", 100f * (float)this.TotalSuccess / (float)attempts);
                }
                else
                {
                    returnValue = "0.0%";
                }

                return returnValue;
            }
        }

        private int _totalAttempts = 0;
        public int TotalAttempts
        {
            get
            {
                return _totalAttempts;
            }
            set
            {
                this.SetProperty(ref _totalAttempts, value);
                this.OnPropertyChanged(nameof(this.PercentSuccess));
            }
        }

        private int _totalSuccess = 0;
        public int TotalSuccess
        {
            get
            {
                return _totalSuccess;
            }
            set
            {
                this.SetProperty(ref _totalSuccess, value);
                this.OnPropertyChanged(nameof(this.PercentSuccess));
                Timee.Text = DateTime.Now.ToString();
                int add = conm.Insert(new Db() { Nem = _humidity, Sicaklik = _temperature, Zaman = DateTime.Now });
                if (_humidity<40|| _humidity>55||_temperature<16||_temperature>24)
                {
                    sayac++;
                }
                else 
                {
                    sayac = 0;
                }
                if (sayac>5)
                {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    SendEMailAsync();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    
                }
                Debug.WriteLine(path);
            }
        }

        private float _humidity = 0f;
        public float Humidity
        {
            get
            {
                return _humidity;
            }

            set
            {
                this.SetProperty(ref _humidity, value);
                this.OnPropertyChanged(nameof(this.HumidityDisplay));
            }
        }

        public string HumidityDisplay
        {

            get
            {

                return string.Format("{0:0.0}% RH", this.Humidity);

            }

        }


        private float _temperature = 0f;
        public float Temperature
        {
            get
            {
                return _temperature;
            }
            set
            {
                this.SetProperty(ref _temperature, value);
                this.OnPropertyChanged(nameof(this.TemperatureDisplay));
            }
        }

        public string TemperatureDisplay
        {

            get
            {

                return string.Format("{0:0.0} °C", this.Temperature);
            }
        }

        private DateTimeOffset _lastUpdated = DateTimeOffset.MinValue;
        public DateTimeOffset LastUpdated
        {
            get
            {
                return _lastUpdated;
            }
            set
            {
                this.SetProperty(ref _lastUpdated, value);
                this.OnPropertyChanged(nameof(this.LastUpdatedDisplay));
            }
        }

        public string LastUpdatedDisplay
        {
            get
            {
                string returnValue = string.Empty;

                TimeSpan elapsed = DateTimeOffset.Now.Subtract(this.LastUpdated);

                if (this.LastUpdated == DateTimeOffset.MinValue)
                {
                    returnValue = "hiçbir zaman";
                }
                else if (elapsed.TotalSeconds < 60d)
                {
                    int seconds = (int)elapsed.TotalSeconds;

                    if (seconds < 2)
                    {
                        returnValue = "Şu anda";
                    }
                    else
                    {
                        returnValue = string.Format("{0:0} {1} önce", seconds, seconds == 1 ? "saniye" : "saniye");
                    }
                }
                else if (elapsed.TotalMinutes < 60d)
                {
                    int minutes = (int)elapsed.TotalMinutes == 0 ? 1 : (int)elapsed.TotalMinutes;
                    returnValue = string.Format("{0:0} {1} önce", minutes, minutes == 1 ? "dakika" : "dakika");
                }
                else if (elapsed.TotalHours < 24d)
                {
                    int hours = (int)elapsed.TotalHours == 0 ? 1 : (int)elapsed.TotalHours;
                    returnValue = string.Format("{0:0} {1} önce", hours, hours == 1 ? "saat" : "saat");
                }
                else
                {
                    returnValue = "uzun zaman önce";
                }

                return returnValue;
            }
        }

        public int AverageRetries
        {
            get
            {
                int returnValue = 0;

                if (_retryCount.Count() > 0)
                {
                    returnValue = (int)_retryCount.Average();
                }

                return returnValue;
            }
        }

        public string AverageRetriesDisplay
        {
            get
            {
                return string.Format("{0:0}", this.AverageRetries);
            }
        }

        public string SuccessRate
        {
            get
            {
                string returnValue = string.Empty;

                double totalSeconds = DateTimeOffset.Now.Subtract(_startedAt).TotalSeconds;
                double rate = this.TotalSuccess / totalSeconds;

                if (rate < 1)
                {
                    returnValue = string.Format("{0:0.00} saniye/okunan", 1d / rate);
                }
                else
                {
                    returnValue = string.Format("{0:0.00} okunan/san", rate);
                }

                return returnValue;
            }
        }

       

        public async Task SendEMailAsync()
        {
            using (SmtpClient client = new SmtpClient("smtp.gmail.com", 465, true, "Gönderici Mail", "Gönderici MailŞifre"))
            {
                if (sayac > 5)
                {
                    EmailMessage emailMessage = new EmailMessage();

                    emailMessage.To.Add(new EmailRecipient("Alıcı Mail adresi"));
                    emailMessage.CC.Add(new EmailRecipient("Alıcı Mail Adresi"));
                    //emailMessage.Bcc.Add(new EmailRecipient("Alıcı Mail Adresi"));
                    emailMessage.Subject = "Server Odası";
                    emailMessage.Body = "Server odasını kontrol etmeniz gerekmektedir beklenmedik durum ile karşı karşıyayız. Server odası sıcaklığı: " + _temperature + " nem oranı: %" + _humidity ;
                    await client.SendMailAsync(emailMessage);

                    var messageDialog = new MessageDialog("Mail Gönderildi...");
                    await messageDialog.ShowAsync();
                }
                
            }
        }
    }
}
