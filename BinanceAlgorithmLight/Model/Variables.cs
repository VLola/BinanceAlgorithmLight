using Binance.Net.Objects.Models.Futures.Socket;
using BinanceAlgorithmLight.Errors;
using BinanceAlgorithmLight.Objects;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BinanceAlgorithmLight.Model
{
    public class Variables : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
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
        private double _AVERAGE_CANDLE;
        public double AVERAGE_CANDLE
        {
            get { return _AVERAGE_CANDLE; }
            set
            {
                _AVERAGE_CANDLE = value;
                OnPropertyChanged("AVERAGE_CANDLE");
            }
        }
        private decimal _PNL;
        public decimal PNL
        {
            get { return _PNL; }
            set
            {
                _PNL = value;
                if (value < 0m) COLOR_PNL = true;
                else COLOR_PNL = false;
                OnPropertyChanged("PNL");
            }
        }
        private bool _COLOR_PNL;
        public bool COLOR_PNL
        {
            get { return _COLOR_PNL; }
            set
            {
                _COLOR_PNL = value;
                OnPropertyChanged("COLOR_PNL");
            }
        }
        private decimal _EXPECTED_PNL = 10.5m;
        public decimal EXPECTED_PNL
        {
            get { return _EXPECTED_PNL; }
            set
            {
                EXPECTED_PNL_CHECK = false;
                _EXPECTED_PNL = value;
                OnPropertyChanged("EXPECTED_PNL");
            }
        }

        private bool _EXPECTED_PNL_CHECK = false;
        public bool EXPECTED_PNL_CHECK
        {
            get { return _EXPECTED_PNL_CHECK; }
            set
            {
                if(value == true && _EXPECTED_PNL > 0m)
                {
                    _EXPECTED_PNL_CHECK = value;
                    OnPropertyChanged("EXPECTED_PNL_CHECK");
                }
                else if (value == false)
                {
                    if (_RESTART_ALGORITHM) RESTART_ALGORITHM = false;
                    _EXPECTED_PNL_CHECK = value;
                    OnPropertyChanged("EXPECTED_PNL_CHECK");
                }
            }
        }
        private decimal _ACCOUNT_BALANCE;
        public decimal ACCOUNT_BALANCE
        {
            get { return _ACCOUNT_BALANCE; }
            set
            {
                _ACCOUNT_BALANCE = Math.Round(value, 3);
                OnPropertyChanged("ACCOUNT_BALANCE");
            }
        }

        private bool _RESTART_ALGORITHM = false;
        public bool RESTART_ALGORITHM
        {
            get { return _RESTART_ALGORITHM; }
            set
            {
                if(value == true) if (!_EXPECTED_PNL_CHECK) EXPECTED_PNL_CHECK = true;
                _RESTART_ALGORITHM = value;
                OnPropertyChanged("RESTART_ALGORITHM");
            }
        }
        private int _COUNT_ORDERS;
        public int COUNT_ORDERS
        {
            get { return _COUNT_ORDERS; }
            set
            {
                _COUNT_ORDERS = value;
                OnPropertyChanged("COUNT_ORDERS");
            }
        }
        private decimal _SUM_PROFIT_ORDERS;
        public decimal SUM_PROFIT_ORDERS
        {
            get { return _SUM_PROFIT_ORDERS; }
            set
            {
                _SUM_PROFIT_ORDERS = value;
                if (value > 0m) COLOR_SUM_PROFIT_ORDERS = "Green";
                else if (value < 0m) COLOR_SUM_PROFIT_ORDERS = "Red";
                else COLOR_SUM_PROFIT_ORDERS = "White";
                OnPropertyChanged("SUM_PROFIT_ORDERS");
            }
        }
        private string _COLOR_SUM_PROFIT_ORDERS = "White";
        public string COLOR_SUM_PROFIT_ORDERS
        {
            get { return _COLOR_SUM_PROFIT_ORDERS; }
            set
            {
                _COLOR_SUM_PROFIT_ORDERS = value;
                OnPropertyChanged("COLOR_SUM_PROFIT_ORDERS");
            }
        }
        private bool _CONNECT_BINANCE = false;
        public bool CONNECT_BINANCE
        {
            get { return _CONNECT_BINANCE; }
            set
            {
                _CONNECT_BINANCE = value;
                OnPropertyChanged("CONNECT_BINANCE");
            }
        }
        private int _CONNECT_BINANCE_SECONDS = 20;
        public int CONNECT_BINANCE_SECONDS
        {
            get { return _CONNECT_BINANCE_SECONDS; }
            set
            {
                if(value > 0)
                {
                    _CONNECT_BINANCE_SECONDS = value;
                    CONNECT_BINANCE_TOOL_TIP = $"after {value} seconds reconnect to binance";
                    OnPropertyChanged("CONNECT_BINANCE_SECONDS");
                }
            }
        }
        private string _CONNECT_BINANCE_TOOL_TIP = "after 10 seconds reconnect to binance";
        public string CONNECT_BINANCE_TOOL_TIP
        {
            get { return _CONNECT_BINANCE_TOOL_TIP; }
            set
            {
                _CONNECT_BINANCE_TOOL_TIP = value;
                OnPropertyChanged("CONNECT_BINANCE_TOOL_TIP");
            }
        }
        private long _PING = 0;
        public long PING
        {
            get { return _PING; }
            set
            {
                if (value > 0)
                {
                    _PING = value;
                    OnPropertyChanged("PING");
                }
            }
        }
        private bool _START_PING = false;
        public bool START_PING
        {
            get { return _START_PING; }
            set
            {
                _START_PING = value;
                OnPropertyChanged("START_PING");
            }
        }
        private bool _CHECK_SUBSCRIBE_TO_ORDER = true;
        public bool CHECK_SUBSCRIBE_TO_ORDER
        {
            get { return _CHECK_SUBSCRIBE_TO_ORDER; }
            set
            {
                _CHECK_SUBSCRIBE_TO_ORDER = value;
                OnPropertyChanged("CHECK_SUBSCRIBE_TO_ORDER");
            }
        }

        private int _ID_SYBSCRIBE_TO_KLINE;
        public int ID_SYBSCRIBE_TO_KLINE
        {
            get { return _ID_SYBSCRIBE_TO_KLINE; }
            set
            {
                _ID_SYBSCRIBE_TO_KLINE = value;
                OnPropertyChanged("ID_SYBSCRIBE_TO_KLINE");
            }
        }
    }
}
