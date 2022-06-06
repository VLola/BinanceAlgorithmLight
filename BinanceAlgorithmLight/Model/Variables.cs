using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BinanceAlgorithmLight.Model
{
    public class Variables : INotifyPropertyChanged
    {
        private decimal _PRICE_SYMBOL;
        public decimal PRICE_SYMBOL
        {
            get { return _PRICE_SYMBOL; }
            set
            {
                _PRICE_SYMBOL = value;
                OnPropertyChanged("PRICE_SYMBOL");
            }
        }
        private bool _START_BET = false;
        public bool START_BET
        {
            get { return _START_BET; }
            set
            {
                if(value == true)
                {
                    if (USDT_CHECK)
                    {
                        _START_BET = value;
                        OnPropertyChanged("START_BET");
                    }
                }
                else
                {
                    _START_BET = value;
                    OnPropertyChanged("START_BET");
                }
            }
        }
        private bool _ONLINE_CHART = false;
        public bool ONLINE_CHART
        {
            get { return _ONLINE_CHART; }
            set
            {
                _ONLINE_CHART = value;
                OnPropertyChanged("ONLINE_CHART");
            }
        }
        private bool _SOUND = false;
        public bool SOUND
        {
            get { return _SOUND; }
            set
            {
                _SOUND = value;
                OnPropertyChanged("SOUND");
            }
        }
        private bool _LONG = false;
        public bool LONG
        {
            get { return _LONG; }
            set
            {
                _LONG = value;
                if (value == true) if (_LINE_OPEN > 0) LINE_OPEN = -_LINE_OPEN;
                OnPropertyChanged("LONG");
            }
        }
        private bool _SHORT = true;
        public bool SHORT
        {
            get { return _SHORT; }
            set
            {
                _SHORT = value; 
                if (value == true) if (_LINE_OPEN < 0) LINE_OPEN = -_LINE_OPEN;
                OnPropertyChanged("SHORT");
            }
        }
        private decimal _MIN_QTY;
        public decimal MIN_QTY
        {
            get { return _MIN_QTY; }
            set
            {
                _MIN_QTY = value;
                OnPropertyChanged("MIN_QTY");
            }
        }
        private decimal _STEP_SIZE;
        public decimal STEP_SIZE
        {
            get { return _STEP_SIZE; }
            set
            {
                _STEP_SIZE = value;
                OnPropertyChanged("STEP_SIZE");
            }
        }
        private decimal _USDT_MIN;
        public decimal USDT_MIN
        {
            get { return _USDT_MIN; }
            set
            {
                _USDT_MIN = value;
                if (_USDT_MIN < _USDT_BET && _USDT_BET > 5m) USDT_CHECK = true;
                else USDT_CHECK = false;
                if (_USDT_MIN > 5m) USDT_MIN_TOOL_TIP = "Minimum - " + _USDT_MIN.ToString();
                else USDT_MIN_TOOL_TIP = "Minimum - 5.1";
                OnPropertyChanged("USDT_MIN");
            }
        }
        private decimal _USDT_BET = 6m;
        public decimal USDT_BET
        {
            get { return _USDT_BET; }
            set
            {
                _USDT_BET = value;
                if (_USDT_MIN < _USDT_BET && _USDT_BET > 5m) USDT_CHECK = true;
                else USDT_CHECK = false;
                OnPropertyChanged("USDT_BET");
            }
        }

        private bool _USDT_CHECK = true;
        public bool USDT_CHECK
        {
            get { return _USDT_CHECK; }
            set
            {
                _USDT_CHECK = value;
                OnPropertyChanged("USDT_CHECK");
            }
        }

        private string _USDT_MIN_TOOL_TIP = "Minimum - 5.1";
        public string USDT_MIN_TOOL_TIP
        {
            get { return _USDT_MIN_TOOL_TIP; }
            set
            {
                _USDT_MIN_TOOL_TIP = value;
                OnPropertyChanged("USDT_MIN_TOOL_TIP");
            }
        }

        private int _LINE_OPEN = 10;
        public int LINE_OPEN
        {
            get { return _LINE_OPEN; }
            set
            {
                _LINE_OPEN = value;
                OnPropertyChanged("LINE_OPEN");
            }
        }
        private decimal _AVERAGE_CANDLE;
        public decimal AVERAGE_CANDLE
        {
            get { return _AVERAGE_CANDLE; }
            set
            {
                _AVERAGE_CANDLE = value;
                OnPropertyChanged("AVERAGE_CANDLE");
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
    }
}
