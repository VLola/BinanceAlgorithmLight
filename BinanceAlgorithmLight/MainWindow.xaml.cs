using Binance.Net.Enums;
using Binance.Net.Objects.Models.Futures;
using Binance.Net.Objects.Models.Futures.Socket;
using Binance.Net.Objects.Models.Spot;
using BinanceAlgorithmLight.Binance;
using BinanceAlgorithmLight.ConnectDB;
using BinanceAlgorithmLight.Errors;
using BinanceAlgorithmLight.Interval;
using BinanceAlgorithmLight.Model;
using BinanceAlgorithmLight.Objects;
using Newtonsoft.Json;
using ScottPlot;
using ScottPlot.Plottable;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Color = System.Drawing.Color;

namespace BinanceAlgorithmLight
{
    public partial class MainWindow : Window
    {
        public FileSystemWatcher error_watcher = new FileSystemWatcher();
        public ErrorText ErrorText = new ErrorText(); 
        public ObservableCollection<OrderHistory> history_list_orders { get; set; } = new ObservableCollection<OrderHistory>();
        public ObservableCollection<OrderHistory> list_orders = new ObservableCollection<OrderHistory>();
        public Variables variables { get; set; } = new Variables();
        public string API_KEY { get; set; } = "";
        public string SECRET_KEY { get; set; } = "";
        public string CLIENT_NAME { get; set; } = "";
        public double LINE_SL { get; set; } = 0.43;
        public int LINE_TP { get; set; } = 100;
        public int COUNT_CANDLES { get; set; } = 100;
        public Socket socket;
        public List<string> list_sumbols_name = new List<string>();
        public KlineInterval interval_time = KlineInterval.OneMinute;
        public TimeSpan timeSpan = new TimeSpan(TimeSpan.TicksPerMinute);
        public List<OHLC> list_ohlc = new List<OHLC>();
        public FinancePlot candlePlot;
        public ScatterPlot line_open_short_scatter;
        public ScatterPlot line_open_1_short_scatter;
        public ScatterPlot line_open_2_short_scatter;
        public ScatterPlot line_open_3_short_scatter;
        public ScatterPlot line_open_long_scatter;
        public ScatterPlot line_open_1_long_scatter;
        public ScatterPlot line_open_2_long_scatter;
        public ScatterPlot line_open_3_long_scatter;
        public ScatterPlot line_sl_long_scatter;
        public ScatterPlot line_sl_short_scatter;
        public ScatterPlot line_tp_1_scatter;
        public ScatterPlot line_tp_2_scatter;
        public ScatterPlot order_long_open_plot;
        public ScatterPlot order_long_close_plot;
        public ScatterPlot order_short_open_plot;
        public ScatterPlot order_short_close_plot;
        public DispatcherTimer timer = new DispatcherTimer();

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
        }

        #region - Main Closed -
        private void MainWindow_Closed(object sender, EventArgs e)
        {
            variables.START_PING = false;
        }
        #endregion

        #region - Main Loaded -
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ErrorWatcher();
            Chart();
            Clients();
            INTERVAL_TIME.ItemsSource = IntervalCandles.Intervals();
            INTERVAL_TIME.SelectedIndex = 0;
            LIST_SYMBOLS.ItemsSource = list_sumbols_name;
            EXIT_GRID.Visibility = Visibility.Hidden;
            LOGIN_GRID.Visibility = Visibility.Visible;
            this.DataContext = this;
            timer.Interval = TimeSpan.FromSeconds(10);
            timer.Tick += Timer_Tick;
        }
        #endregion

        #region - Ping (Thread) -
        private void Ping()
        {
            try
            {
                variables.START_PING = true;
                new Thread(() => { ThreadPing(); }).Start();
            }
            catch (Exception c)
            {
                Error($"Ping {c.Message}");
            }
        }

        private void ThreadPing()
        {
            try
            {
                while(variables.START_PING)
                {
                    if (!PingAsync().Result) {
                        Ping();
                        break; 
                    }
                }
            }
            catch (Exception c)
            {
                Error($"ThreadPing {c.Message}");
            }
        }
        private async Task<bool> PingAsync()
        {
            try
            {
                var result = await socket.futures.ExchangeData.PingAsync();
                if (!result.Success) { 
                    variables.PING = 100000; 
                    return false; 
                }
                else
                {
                    variables.PING = result.Data;
                    return true;
                }
            }
            catch (Exception c)
            {
                Error($"PingAsync {c.Message}");
                return false;
            }
        }
        #endregion

        #region - Timer Reconnect Binance -
        private void Timer_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (variables.CONNECT_BINANCE_SECONDS > 0 && variables.START_PING)
                {
                    timer.Stop();
                    timer.Interval = TimeSpan.FromSeconds(variables.CONNECT_BINANCE_SECONDS);
                    timer.Start();
                }
            }
            catch (Exception c)
            {
                Error($"Timer_TextChanged {c.Message}");
            }
        }
        void Timer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (!variables.CONNECT_BINANCE && variables.ONLINE_CHART || !variables.CHECK_SUBSCRIBE_TO_ORDER && variables.ONLINE_CHART)
                {
                    variables.CHECK_SUBSCRIBE_TO_ORDER = true;
                    UnsubscribeAllAsync();
                    SubscribeToKline(); 
                    SubscribeToOrderThread();
                }
                variables.CONNECT_BINANCE = false;
                if (!variables.START_PING) Ping();
            }
            catch (Exception c)
            {
                Error($"Timer_Tick {c.Message}");
            }
        }
        #endregion

        #region - Trade history (Async) -
        private async void TradeHistoryAsync()
        {
            try
            {
                await Task.Run(()=> {
                    decimal sum_profit_orders = 0m;
                    int count_orders = 0;
                    variables.SUM_PROFIT_ORDERS = 0m;
                    variables.COUNT_ORDERS = 0;
                    foreach (var it in history_list_orders)
                    {
                        sum_profit_orders += it.RealizedProfit;
                        sum_profit_orders -= it.Commission;
                        count_orders++;
                    }
                    variables.SUM_PROFIT_ORDERS = sum_profit_orders;
                    variables.COUNT_ORDERS = count_orders;
                });
            }
            catch (Exception c)
            {
                Error($"TradeHistory {c.Message}");
            }
        }
        #endregion

        #region - Average Candle (Async) -
        private void Average_Click(object sender, RoutedEventArgs e)
        {
            AverageCandleAsync();
        }
        private async void AverageCandleAsync()
        {
            try
            {
                await Task.Run(()=> {
                    double sum_low_high_price = 0;
                    foreach (var it in list_ohlc)
                    {
                        sum_low_high_price += (it.High - it.Low);
                    }
                    variables.AVERAGE_CANDLE = Math.Round((sum_low_high_price / (list_ohlc.Count - 1)) / (Decimal.ToDouble(variables.PRICE_SYMBOL) / 1000));
                });
            }
            catch (Exception c)
            {
                Error($"AverageCandleAsync {c.Message}");
            }
        }
        #endregion

        #region - Balance (Async) -
        public async void BalanceFutureAsync()
        {
            try
            {
                await Task.Run(() => {
                    var result = socket.futures.Account.GetAccountInfoAsync().Result;
                    if (!result.Success)
                    {
                        Error($"Failed BalanceFutureAsync: {result.Error.Message}");
                    }
                    else
                    {
                        variables.ACCOUNT_BALANCE = result.Data.TotalMarginBalance;
                    }
                });
            }
            catch (Exception c)
            {
                Error($"BalanceFutureAsync {c.Message}");
            }
        }
        #endregion

        #region - Subscribe To Order -

        private void ClearListOrders()
        {
            if (list_orders.Count > 0) list_orders.Clear();
        }
        //private void PriceOrder(BinanceFuturesStreamOrderUpdateData order)
        //{
        //    try
        //    {
        //        if (order.PositionSide == PositionSide.Long && order.Side == OrderSide.Buy || order.PositionSide == PositionSide.Short && order.Side == OrderSide.Sell)
        //        {
        //            if (order.OrderId == short_order_id_0)
        //            {
        //                short_price_order_0 = order.AveragePrice;
        //                NewLines(Double.Parse(short_price_order_0.ToString()));
        //            }
        //            else if (order.OrderId == short_order_id_1)
        //            {
        //                short_price_order_1 = order.AveragePrice;
        //                decimal average = Math.Round(((short_quantity_0 * short_price_order_0) + (short_quantity_1 * short_price_order_1)) / (short_quantity_1 + short_quantity_0), 6);
        //                NewLineSLShort(Decimal.ToDouble(average));
        //                permission_to_close_orders = true;
        //            }
        //            else if (order.OrderId == short_order_id_2)
        //            {
        //                short_price_order_2 = order.AveragePrice;
        //                decimal average = Math.Round(((short_quantity_0 * short_price_order_0) + (short_quantity_1 * short_price_order_1) + (short_quantity_2 * short_price_order_2)) / (short_quantity_1 + short_quantity_2 + short_quantity_0), 6);
        //                NewLineSLShort(Decimal.ToDouble(average));
        //                permission_to_close_orders = true;
        //            }
        //            else if (order.OrderId == short_order_id_3)
        //            {
        //                short_price_order_3 = order.AveragePrice;
        //                decimal average = Math.Round(((short_quantity_0 * short_price_order_0) + (short_quantity_1 * short_price_order_1) + (short_quantity_2 * short_price_order_2) + (short_quantity_3 * short_price_order_3)) / (short_quantity_1 + short_quantity_2 + short_quantity_3 + short_quantity_0), 6);
        //                NewLineSLShort(Decimal.ToDouble(average));
        //                permission_to_close_orders = true;
        //            }
        //        }
        //        else if (order.OrderId == short_close_order_id)
        //        {
        //            short_close_order_id = 0;
        //            short_price_order_0 = Decimal.Parse(line_sl_short_y[0].ToString());
        //            NewLines(line_sl_short_y[0]);
        //            NewLineSLShortClear();
        //        }
        //    }
        //    catch (Exception c)
        //    {
        //        Error($"PriceOrder {c.Message}");
        //    }
        //}
        public void SubscribeToOrderThread()
        {
            try
            {
                string symbol = LIST_SYMBOLS.Text;
                new Thread(() => { SubscribeToOrderAsync(symbol); }).Start();
            }
            catch (Exception c)
            {
                Error($"SubscribeToOrderThread {c.Message}");
            }
        }
        public async void SubscribeToOrderAsync(string symbol)
        {
            try
            {
                bool unsubscribe = true;
                //Error($"Subscribe to orders {symbol}");
                var listenKey = await socket.futures.Account.StartUserStreamAsync();
                if (!listenKey.Success) {
                    Error($"Failed to start user stream: listenKey");
                    Dispatcher.Invoke(new Action(() =>
                    {
                        variables.CHECK_SUBSCRIBE_TO_ORDER = false;
                    }));
                }
                else
                {
                    var result = await socket.socketClient.UsdFuturesStreams.SubscribeToUserDataUpdatesAsync(listenKey: listenKey.Data,
                    onLeverageUpdate => { },
                    onMarginUpdate => { },
                    onAccountUpdate => {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            variables.ACCOUNT_BALANCE = onAccountUpdate.Data.UpdateData.Balances.ToList()[0].CrossWalletBalance;
                        }));
                    },
                    onOrderUpdate =>
                    {
                        //if (onOrderUpdate.Data.UpdateData.Symbol == symbol && onOrderUpdate.Data.UpdateData.Status == OrderStatus.Filled || onOrderUpdate.Data.UpdateData.Symbol == symbol && onOrderUpdate.Data.UpdateData.Status == OrderStatus.PartiallyFilled)
                        //{
                        //    Dispatcher.BeginInvoke(new Action(() =>
                        //    {
                        //        if (onOrderUpdate.Data.UpdateData.OrderId == short_order_id_0 && !CheckOrderIdToListOrders() || onOrderUpdate.Data.UpdateData.OrderId == long_order_id_0 && !CheckOrderIdToListOrders()) ClearListOrders();
                        //        list_orders.Add(onOrderUpdate.Data.UpdateData);
                        //        history_list_orders.Add(new OrderHistory(onOrderUpdate.Data.UpdateData));
                        //        HISTORY_LIST.Items.Refresh();
                        //        PriceOrder(onOrderUpdate.Data.UpdateData);
                        //        LoadingChartOrders();
                        //        TradeHistoryAsync();
                        //    }));
                        //}
                    },
                    onListenKeyExpired => {
                        if (unsubscribe)
                        {
                            //Error($"Listen Key Expired {symbol}");
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                variables.CHECK_SUBSCRIBE_TO_ORDER = false;
                            }));
                        }
                        unsubscribe = false;
                    }
                    );
                    if (!result.Success)
                    {
                        Error($"Failed UserDataUpdates: {result.Error.Message}");
                        Dispatcher.Invoke(new Action(() =>
                        {
                            variables.CHECK_SUBSCRIBE_TO_ORDER = false;
                        }));
                    }
                }
            }
            catch (Exception c)
            {
                Error($"SubscribeToOrder {c.Message}");
                Dispatcher.Invoke(new Action(() =>
                {
                    SubscribeToOrderThread();
                }));
            }
        }

        //private bool CheckOrderIdToListOrders()
        //{
        //    foreach(var it in list_orders)
        //    {
        //        if (it.OrderId == short_order_id_0 || it.OrderId == long_order_id_0) return true;
        //    }
        //    return false;
        //}
        #endregion

        #region - Subscribe To Kline -
        private void START_ASYNC_Click(object sender, RoutedEventArgs e)
        {
            SubscribeToKline();
            SubscribeToOrderThread();
        }
        private void STOP_ASYNC_Click(object sender, RoutedEventArgs e)
        {
            UnsubscribeAllAsync();
        }
        private void UnsubscribeAllAsync()
        {
            try
            {
                socket.socketClient.UnsubscribeAllAsync();
            }
            catch (Exception c)
            {
                Error($"STOP_ASYNC_Click {c.Message}");
            }
        }
        public async void SubscribeToKline()
        {
            try
            {
                variables.CONNECT_BINANCE = true;
                var result = await socket.socketClient.UsdFuturesStreams.SubscribeToKlineUpdatesAsync(LIST_SYMBOLS.Text, interval_time, Message =>
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if(!variables.CONNECT_BINANCE) variables.CONNECT_BINANCE = true;
                        variables.PRICE_SYMBOL = Message.Data.Data.ClosePrice;
                        UpdateListOHLC(new OHLC(Decimal.ToDouble(Message.Data.Data.OpenPrice), Decimal.ToDouble(Message.Data.Data.HighPrice), Decimal.ToDouble(Message.Data.Data.LowPrice), Decimal.ToDouble(Message.Data.Data.ClosePrice), Message.Data.Data.OpenTime, timeSpan));
                        LoadingChart();
                        plt.Render();
                        if (variables.EXPECTED_PNL_CHECK && variables.PNL > variables.EXPECTED_PNL && variables.RESTART_ALGORITHM)
                        {
                            CloseOrders();
                            variables.START_BET = true;
                            OpenOrders();
                        }
                        else if (variables.EXPECTED_PNL_CHECK && variables.PNL > variables.EXPECTED_PNL)
                        {
                            CloseOrders();
                        }
                    }));
                });
                if (!result.Success)
                {
                    Error($"Failed SubscribeToKline: {result.Error.Message}");
                    Dispatcher.Invoke(new Action(() =>
                    {
                        variables.CONNECT_BINANCE = false;
                    }));
                }
            }
            catch (Exception c)
            {
                Error($"STOP_ASYNC_Click {c.Message}");
                Dispatcher.Invoke(new Action(() =>
                {
                    variables.CONNECT_BINANCE = false;
                }));
            }
        }
        private void UpdateListOHLC(OHLC ohlc)
        {
            if (ohlc.DateTime == list_ohlc[list_ohlc.Count - 1].DateTime) list_ohlc.RemoveAt(list_ohlc.Count - 1);
            list_ohlc.Add(ohlc);
        }
        #endregion

        #region - Symbol Info -
        private void ExchangeInfo()
        {
            try
            {
                string symbol = LIST_SYMBOLS.Text;
                var result = socket.futures.ExchangeData.GetExchangeInfoAsync();
                if (!result.Result.Success) Error("Error ExchangeInfo");
                else
                {
                    List<BinanceFuturesUsdtSymbol> list = result.Result.Data.Symbols.ToList();
                    foreach (var it in list)
                    {
                        if (symbol == it.Name)
                        {
                            variables.MIN_QTY = it.LotSizeFilter.MinQuantity;
                            variables.STEP_SIZE = it.LotSizeFilter.StepSize;
                            break;
                        }
                    }
                    variables.USDT_MIN = variables.MIN_QTY * variables.PRICE_SYMBOL;
                }
            }
            catch (Exception c)
            {
                Error($"ExchangeInfo {c.Message}");
            }
        }
        #endregion

        #region - Orders -
        List<double> long_open_order_x = new List<double>();
        List<double> long_open_order_y = new List<double>();
        List<double> long_close_order_x = new List<double>();
        List<double> long_close_order_y = new List<double>();
        List<double> short_open_order_x = new List<double>();
        List<double> short_open_order_y = new List<double>();
        List<double> short_close_order_x = new List<double>();
        List<double> short_close_order_y = new List<double>();
        private void CoordinatesOrders()
        {
            long_open_order_x.Clear();
            long_open_order_y.Clear();
            long_close_order_x.Clear();
            long_close_order_y.Clear();
            short_open_order_x.Clear();
            short_open_order_y.Clear();
            short_close_order_x.Clear();
            short_close_order_y.Clear();
            foreach (var it in list_orders)
            {
                if (it.PositionSide == PositionSide.Long && it.Side == OrderSide.Buy)
                {
                    long_open_order_x.Add(it.UpdateTime.ToOADate());
                    long_open_order_y.Add(Double.Parse(it.AvgPrice.ToString()));
                }
                if (it.PositionSide == PositionSide.Long && it.Side == OrderSide.Sell)
                {
                    long_close_order_x.Add(it.UpdateTime.ToOADate());
                    long_close_order_y.Add(Double.Parse(it.AvgPrice.ToString()));
                }
                if (it.PositionSide == PositionSide.Short && it.Side == OrderSide.Sell)
                {
                    short_open_order_x.Add(it.UpdateTime.ToOADate());
                    short_open_order_y.Add(Double.Parse(it.AvgPrice.ToString()));
                }
                if (it.PositionSide == PositionSide.Short && it.Side == OrderSide.Buy)
                {
                    short_close_order_x.Add(it.UpdateTime.ToOADate());
                    short_close_order_y.Add(Double.Parse(it.AvgPrice.ToString()));
                }
            }
        }
        private void ChartPointsOrders()
        {
            plt.Plot.Remove(order_long_open_plot);
            plt.Plot.Remove(order_long_close_plot);
            plt.Plot.Remove(order_short_open_plot);
            plt.Plot.Remove(order_short_close_plot);
            if(long_open_order_x.Count > 0)
            {
                order_long_open_plot = plt.Plot.AddScatter(long_open_order_x.ToArray(), long_open_order_y.ToArray(), color: Color.Green, lineWidth: 0, markerSize: 8);
                order_long_open_plot.YAxisIndex = 1;
            }
            if (long_close_order_x.Count > 0)
            {
                order_long_close_plot = plt.Plot.AddScatter(long_close_order_x.ToArray(), long_close_order_y.ToArray(), color: Color.Orange, lineWidth: 0, markerSize: 10, markerShape: MarkerShape.eks);
                order_long_close_plot.YAxisIndex = 1;
            }
            if (short_open_order_x.Count > 0)
            {
                order_short_open_plot = plt.Plot.AddScatter(short_open_order_x.ToArray(), short_open_order_y.ToArray(), color: Color.DarkRed, lineWidth: 0, markerSize: 8);
                order_short_open_plot.YAxisIndex = 1;
            }
            if (short_close_order_x.Count > 0)
            {
                order_short_close_plot = plt.Plot.AddScatter(short_close_order_x.ToArray(), short_close_order_y.ToArray(), color: Color.Orange, lineWidth: 0, markerSize: 10, markerShape: MarkerShape.eks);
                order_short_close_plot.YAxisIndex = 1;
            }
        }
        private void LoadingChartOrders()
        {
            CoordinatesOrders();
            ChartPointsOrders();
        }
        #endregion

        #region - Open order, close order -
        private void OpenOrders_Click(object sender, RoutedEventArgs e)
        {
            OpenOrders();
        }
        private void OpenOrders()
        {
            try
            {
                string symbol = LIST_SYMBOLS.Text;
                if (variables.START_BET && variables.PRICE_SYMBOL > 0m && variables.LINE_OPEN > 0 && short_order_id_0 == 0 && long_order_id_0 == 0)
                {
                    ClearListOrders();
                    short_quantity_0 = RoundQuantity(variables.USDT_BET * 2 / variables.PRICE_SYMBOL);
                    short_order_id_0 = Algorithm.Algorithm.Order(socket, symbol, OrderSide.Sell, FuturesOrderType.Market, short_quantity_0, PositionSide.Short);
                    long_quantity_0 = short_quantity_0;
                    long_order_id_0 = Algorithm.Algorithm.Order(socket, symbol, OrderSide.Buy, FuturesOrderType.Market, long_quantity_0, PositionSide.Long);
                    if (short_order_id_0 != 0 && long_order_id_0 != 0)
                    {
                        PriceOrder(socket, symbol, short_order_id_0, 0m);
                        PriceOrder(socket, symbol, long_order_id_0, 0m);
                        start = true;
                        SoundOpenOrder();
                    }
                    else if(short_order_id_0 == 0) Error($"Filed open order short!");
                    else if(long_order_id_0 == 0) Error($"Filed open order long!");
                }
            }
            catch (Exception c)
            {
                Error($"OpenOrders {c.Message}");
            }
        }

        private void CloseOrders_Click(object sender, RoutedEventArgs e)
        {
            CloseOrders();
        }


        private void CloseOrders()
        {
            try
            {
                string symbol = LIST_SYMBOLS.Text;
                if (short_order_id_0 != 0 && short_order_id_1 != 0 && short_order_id_2 != 0 && short_order_id_3 != 0)
                {
                    long id = Algorithm.Algorithm.Order(socket, symbol, OrderSide.Buy, FuturesOrderType.Market, short_quantity_0 + short_quantity_1 + short_quantity_2 + short_quantity_3, PositionSide.Short);
                    short_order_id_0 = 0;
                    short_quantity_0 = 0m;
                    short_order_id_1 = 0;
                    short_quantity_1 = 0m;
                    short_order_id_2 = 0;
                    short_quantity_2 = 0m;
                    short_order_id_3 = 0;
                    short_quantity_3 = 0m;
                    SoundCloseOrder();
                    OrderToList(socket, symbol, id, 0m);
                }
                else if (short_order_id_0 != 0 && short_order_id_1 != 0 && short_order_id_2 != 0)
                {
                    long id = Algorithm.Algorithm.Order(socket, symbol, OrderSide.Buy, FuturesOrderType.Market, short_quantity_0 + short_quantity_1 + short_quantity_2, PositionSide.Short);
                    short_order_id_0 = 0;
                    short_quantity_0 = 0m;
                    short_order_id_1 = 0;
                    short_quantity_1 = 0m;
                    short_order_id_2 = 0;
                    short_quantity_2 = 0m;
                    SoundCloseOrder();
                    OrderToList(socket, symbol, id, 0m);
                }
                else if (short_order_id_0 != 0 && short_order_id_1 != 0)
                {
                    long id = Algorithm.Algorithm.Order(socket, symbol, OrderSide.Buy, FuturesOrderType.Market, short_quantity_0 + short_quantity_1, PositionSide.Short);
                    short_order_id_0 = 0;
                    short_quantity_0 = 0m;
                    short_order_id_1 = 0;
                    short_quantity_1 = 0m;
                    SoundCloseOrder();
                    OrderToList(socket, symbol, id, 0m);
                }
                else if (short_order_id_0 != 0)
                {
                    long id = Algorithm.Algorithm.Order(socket, symbol, OrderSide.Buy, FuturesOrderType.Market, short_quantity_0, PositionSide.Short);
                    short_order_id_0 = 0;
                    short_quantity_0 = 0m;
                    SoundCloseOrder();
                    OrderToList(socket, symbol, id, 0m);
                }
                if (long_order_id_0 != 0 && long_order_id_1 != 0 && long_order_id_2 != 0 && long_order_id_3 != 0)
                {
                    long id = Algorithm.Algorithm.Order(socket, symbol, OrderSide.Sell, FuturesOrderType.Market, long_quantity_0 + long_quantity_1 + long_quantity_2 + long_quantity_3, PositionSide.Long);
                    long_order_id_0 = 0;
                    long_quantity_0 = 0m;
                    long_order_id_1 = 0;
                    long_quantity_1 = 0m;
                    long_order_id_2 = 0;
                    long_quantity_2 = 0m;
                    long_order_id_3 = 0;
                    long_quantity_3 = 0m;
                    SoundCloseOrder();
                    OrderToList(socket, symbol, id, 0m);
                }
                else if (long_order_id_0 != 0 && long_order_id_1 != 0 && long_order_id_2 != 0)
                {
                    long id = Algorithm.Algorithm.Order(socket, symbol, OrderSide.Sell, FuturesOrderType.Market, long_quantity_0 + long_quantity_1 + long_quantity_2, PositionSide.Long);
                    long_order_id_0 = 0;
                    long_quantity_0 = 0m;
                    long_order_id_1 = 0;
                    long_quantity_1 = 0m;
                    long_order_id_2 = 0;
                    long_quantity_2 = 0m;
                    SoundCloseOrder();
                    OrderToList(socket, symbol, id, 0m);
                }
                else if (long_order_id_0 != 0 && long_order_id_1 != 0)
                {
                    long id = Algorithm.Algorithm.Order(socket, symbol, OrderSide.Sell, FuturesOrderType.Market, long_quantity_0 + long_quantity_1, PositionSide.Long);
                    long_order_id_0 = 0;
                    long_quantity_0 = 0m;
                    long_order_id_1 = 0;
                    long_quantity_1 = 0m;
                    SoundCloseOrder();
                    OrderToList(socket, symbol, id, 0m);
                }
                else if (long_order_id_0 != 0)
                {
                    long id = Algorithm.Algorithm.Order(socket, symbol, OrderSide.Sell, FuturesOrderType.Market, long_quantity_0, PositionSide.Long);
                    long_order_id_0 = 0;
                    long_quantity_0 = 0m;
                    SoundCloseOrder();
                    OrderToList(socket, symbol, id, 0m);
                }
                NewLineSLShortClear();
                NewLineSLLongClear();
                ReloadSettings();
            }
            catch (Exception c)
            {
                Error($"CloseOrders {c.Message}");
            }
        }
        #endregion

        #region - Algorithm -

        private decimal RoundQuantity(decimal quantity)
        {
            decimal quantity_final = 0m;
            if (variables.STEP_SIZE == 0.001m) quantity_final = Math.Round(quantity, 3);
            else if (variables.STEP_SIZE == 0.01m) quantity_final = Math.Round(quantity, 2);
            else if (variables.STEP_SIZE == 0.1m) quantity_final = Math.Round(quantity, 1);
            else if (variables.STEP_SIZE == 1m) quantity_final = Math.Round(quantity, 0);
            return quantity_final;
        }

        private void ReloadSettings()
        {
            short_order_id_0 = 0;
            long_order_id_0 = 0;
            short_close_order_id = 0;
            short_quantity_0 = 0m;
            long_quantity_0 = 0m;
            short_order_id_1 = 0;
            short_order_id_2 = 0;
            short_order_id_3 = 0;
            short_quantity_1 = 0m;
            short_quantity_2 = 0m;
            short_quantity_3 = 0m;
            long_order_id_1 = 0;
            long_order_id_2 = 0;
            long_order_id_3 = 0;
            long_quantity_1 = 0m;
            long_quantity_2 = 0m;
            long_quantity_3 = 0m;
            start = false;
            variables.START_BET = false;
            variables.PNL = 0m;
        }
        public long short_order_id_0 = 0;
        public long short_order_id_1 = 0;
        public long short_order_id_2 = 0;
        public long short_order_id_3 = 0;
        public long short_close_order_id = 0;
        public decimal short_quantity_0 = 0m;
        public decimal short_quantity_1 = 0m;
        public decimal short_quantity_2 = 0m;
        public decimal short_quantity_3 = 0m;
        public decimal short_price_order_0;
        public decimal short_price_order_1;
        public decimal short_price_order_2;
        public decimal short_price_order_3;
        public long long_order_id_0 = 0;
        public long long_order_id_1 = 0;
        public long long_order_id_2 = 0;
        public long long_order_id_3 = 0;
        public long long_close_order_id = 0;
        public decimal long_quantity_0 = 0m;
        public decimal long_quantity_1 = 0m;
        public decimal long_quantity_2 = 0m;
        public decimal long_quantity_3 = 0m; 
        public decimal long_price_order_0;
        public decimal long_price_order_1;
        public decimal long_price_order_2;
        public decimal long_price_order_3;
        public bool start = false;
        private void StartAlgorithm()
        {
            try
            {
                string symbol = LIST_SYMBOLS.Text;
                if (variables.ONLINE_CHART && variables.START_BET && start)
                {
                    // Short
                    if (short_order_id_1 == 0 && list_ohlc[list_ohlc.Count - 1].Close > line_open_1_y_short[0])
                    {
                        short_quantity_1 = RoundQuantity(short_quantity_0 * 0.75m);
                        short_order_id_1 = Algorithm.Algorithm.Order(socket, symbol, OrderSide.Sell, FuturesOrderType.Market, short_quantity_1, PositionSide.Short);
                        SoundOpenOrder();
                        PriceOrder(socket, symbol, short_order_id_1, 0m);
                    }
                    if (short_order_id_2 == 0 && list_ohlc[list_ohlc.Count - 1].Close > line_open_2_y_short[0])
                    {
                        short_quantity_2 = RoundQuantity(short_quantity_0 * 0.6m);
                        short_order_id_2 = Algorithm.Algorithm.Order(socket, symbol, OrderSide.Sell, FuturesOrderType.Market, short_quantity_2, PositionSide.Short);
                        SoundOpenOrder();
                        PriceOrder(socket, symbol, short_order_id_2, 0m);
                    }
                    if (short_order_id_3 == 0 && list_ohlc[list_ohlc.Count - 1].Close > line_open_3_y_short[0])
                    {
                        short_quantity_3 = RoundQuantity(short_quantity_0 * 0.5m);
                        short_order_id_3 = Algorithm.Algorithm.Order(socket, symbol, OrderSide.Sell, FuturesOrderType.Market, short_quantity_3, PositionSide.Short);
                        SoundOpenOrder();
                        PriceOrder(socket, symbol, short_order_id_3, 0m);
                    }
                    if (list_ohlc[list_ohlc.Count - 1].Close < line_sl_short_y[0])
                    {
                        decimal sl = Decimal.Parse(line_sl_short_y[0].ToString());
                        if (short_order_id_1 != 0 && short_order_id_2 != 0 && short_order_id_3 != 0)
                        {
                            short_close_order_id = Algorithm.Algorithm.Order(socket, symbol, OrderSide.Buy, FuturesOrderType.Market, short_quantity_1 + short_quantity_2 + short_quantity_3, PositionSide.Short);
                            short_quantity_1 = 0m;
                            short_order_id_1 = 0;
                            short_quantity_2 = 0m;
                            short_order_id_2 = 0;
                            short_quantity_3 = 0m;
                            short_order_id_3 = 0;
                            SoundCloseOrder();
                            PriceOrder(socket, symbol, short_close_order_id, sl);
                        }
                        else if (short_order_id_1 != 0 && short_order_id_2 != 0)
                        {
                            short_close_order_id = Algorithm.Algorithm.Order(socket, symbol, OrderSide.Buy, FuturesOrderType.Market, short_quantity_1 + short_quantity_2, PositionSide.Short);
                            short_quantity_1 = 0m;
                            short_order_id_1 = 0;
                            short_quantity_2 = 0m;
                            short_order_id_2 = 0;
                            SoundCloseOrder();
                            PriceOrder(socket, symbol, short_close_order_id, sl);
                        }
                        else if (short_order_id_1 != 0)
                        {
                            short_close_order_id = Algorithm.Algorithm.Order(socket, symbol, OrderSide.Buy, FuturesOrderType.Market, short_quantity_1, PositionSide.Short);
                            short_quantity_1 = 0m;
                            short_order_id_1 = 0;
                            SoundCloseOrder();
                            PriceOrder(socket, symbol, short_close_order_id, sl);
                        }
                    }

                    // Long
                    if (long_order_id_1 == 0 && list_ohlc[list_ohlc.Count - 1].Close < line_open_1_y_long[0])
                    {
                        long_quantity_1 = RoundQuantity(long_quantity_0 * 0.75m);
                        long_order_id_1 = Algorithm.Algorithm.Order(socket, symbol, OrderSide.Buy, FuturesOrderType.Market, long_quantity_1, PositionSide.Long);
                        SoundOpenOrder();
                        PriceOrder(socket, symbol, long_order_id_1, 0m);
                    }
                    if (long_order_id_2 == 0 && list_ohlc[list_ohlc.Count - 1].Close < line_open_2_y_long[0])
                    {
                        long_quantity_2 = RoundQuantity(long_quantity_0 * 0.6m);
                        long_order_id_2 = Algorithm.Algorithm.Order(socket, symbol, OrderSide.Buy, FuturesOrderType.Market, long_quantity_2, PositionSide.Long);
                        SoundOpenOrder();
                        PriceOrder(socket, symbol, long_order_id_2, 0m);
                    }
                    if (long_order_id_3 == 0 && list_ohlc[list_ohlc.Count - 1].Close < line_open_3_y_long[0])
                    {
                        long_quantity_3 = RoundQuantity(long_quantity_0 * 0.5m);
                        long_order_id_3 = Algorithm.Algorithm.Order(socket, symbol, OrderSide.Buy, FuturesOrderType.Market, long_quantity_3, PositionSide.Long);
                        SoundOpenOrder();
                        PriceOrder(socket, symbol, long_order_id_3, 0m);
                    }
                    if (line_sl_long_y[0] > 0d && list_ohlc[list_ohlc.Count - 1].Close > line_sl_long_y[0])
                    {
                        decimal sl = Decimal.Parse(line_sl_long_y[0].ToString());
                        if (long_order_id_1 != 0 && long_order_id_2 != 0 && long_order_id_3 != 0)
                        {
                            long_close_order_id = Algorithm.Algorithm.Order(socket, symbol, OrderSide.Sell, FuturesOrderType.Market, long_quantity_1 + long_quantity_2 + long_quantity_3, PositionSide.Long);
                            long_quantity_1 = 0m;
                            long_order_id_1 = 0;
                            long_quantity_2 = 0m;
                            long_order_id_2 = 0;
                            long_quantity_3 = 0m;
                            long_order_id_3 = 0;
                            SoundCloseOrder();
                            PriceOrder(socket, symbol, long_close_order_id, sl);
                        }
                        else if (long_order_id_1 != 0 && long_order_id_2 != 0)
                        {
                            long_close_order_id = Algorithm.Algorithm.Order(socket, symbol, OrderSide.Sell, FuturesOrderType.Market, long_quantity_1 + long_quantity_2, PositionSide.Long);
                            long_quantity_1 = 0m;
                            long_order_id_1 = 0;
                            long_quantity_2 = 0m;
                            long_order_id_2 = 0;
                            SoundCloseOrder();
                            PriceOrder(socket, symbol, long_close_order_id, sl);
                        }
                        else if (long_order_id_1 != 0)
                        {
                            long_close_order_id = Algorithm.Algorithm.Order(socket, symbol, OrderSide.Sell, FuturesOrderType.Market, long_quantity_1, PositionSide.Long);
                            long_quantity_1 = 0m;
                            long_order_id_1 = 0;
                            SoundCloseOrder();
                            PriceOrder(socket, symbol, long_close_order_id, sl);
                        }
                    }


                    // Take profit
                    if (short_order_id_0 != 0 && long_order_id_0 != 0 && list_ohlc[list_ohlc.Count - 1].Close > line_tp_1_y[0])
                    {
                        CloseOrders();
                    }
                    if (short_order_id_0 != 0 && long_order_id_0 != 0 && list_ohlc[list_ohlc.Count - 1].Close < line_tp_2_y[0])
                    {
                        CloseOrders();
                    }
                    CalculatePnl(variables.PRICE_SYMBOL);
                }
            }
            catch (Exception c)
            {
                Error($"StartAlgorithm {c.Message}");
            }
        }
        public async void PriceOrder(Socket socket, string symbol, long orderId, decimal StartPrice)
        {
            if (orderId == short_order_id_0)
            {
                await Task.Run(() => {
                    BinanceFuturesOrder binanceFuturesOrder = InfoOrder(socket, symbol, orderId);
                    OrderHistory order = new OrderHistory(binanceFuturesOrder, StartPrice);
                    Dispatcher.Invoke(new Action(() => {
                        short_price_order_0 = order.AvgPrice;
                        NewLinesShort(Double.Parse(short_price_order_0.ToString()));
                        list_orders.Add(order);
                        history_list_orders.Add(order);
                        LoadingChartOrders();
                    }));
                });
            }
            else if (orderId == short_order_id_1)
            {
                await Task.Run(() => {
                    BinanceFuturesOrder binanceFuturesOrder = InfoOrder(socket, symbol, orderId);
                    OrderHistory order = new OrderHistory(binanceFuturesOrder, StartPrice);
                    Dispatcher.Invoke(new Action(() => {
                        short_price_order_1 = order.AvgPrice;
                        decimal average = Math.Round(((short_quantity_0 * short_price_order_0) + (short_quantity_1 * short_price_order_1)) / (short_quantity_1 + short_quantity_0), 6);
                        NewLineSLShort(Decimal.ToDouble(average));
                        list_orders.Add(order);
                        history_list_orders.Add(order);
                        LoadingChartOrders();
                    }));
                });
            }
            else if (orderId == short_order_id_2)
            {
                await Task.Run(() => {
                    BinanceFuturesOrder binanceFuturesOrder = InfoOrder(socket, symbol, orderId);
                    OrderHistory order = new OrderHistory(binanceFuturesOrder, StartPrice);
                    Dispatcher.Invoke(new Action(() => {
                        short_price_order_2 = order.AvgPrice;
                        decimal average = Math.Round(((short_quantity_0 * short_price_order_0) + (short_quantity_1 * short_price_order_1) + (short_quantity_2 * short_price_order_2)) / (short_quantity_1 + short_quantity_2 + short_quantity_0), 6);
                        NewLineSLShort(Decimal.ToDouble(average));
                        list_orders.Add(order);
                        history_list_orders.Add(order);
                        LoadingChartOrders();
                    }));
                });
            }
            else if (orderId == short_order_id_3)
            {
                await Task.Run(() => {
                    BinanceFuturesOrder binanceFuturesOrder = InfoOrder(socket, symbol, orderId);
                    OrderHistory order = new OrderHistory(binanceFuturesOrder, StartPrice);
                    Dispatcher.Invoke(new Action(() => {
                        short_price_order_3 = order.AvgPrice;
                        decimal average = Math.Round(((short_quantity_0 * short_price_order_0) + (short_quantity_1 * short_price_order_1) + (short_quantity_2 * short_price_order_2) + (short_quantity_3 * short_price_order_3)) / (short_quantity_1 + short_quantity_2 + short_quantity_3 + short_quantity_0), 6);
                        NewLineSLShort(Decimal.ToDouble(average));
                        list_orders.Add(order);
                        history_list_orders.Add(order);
                        LoadingChartOrders();
                    }));
                });
            }
            else if (orderId == short_close_order_id)
            {
                short_close_order_id = 0;
                short_price_order_0 = Decimal.Parse(line_sl_short_y[0].ToString());
                NewLinesShort(line_sl_short_y[0]);
                NewLineSLShortClear();
                await Task.Run(() => {
                    BinanceFuturesOrder binanceFuturesOrder = InfoOrder(socket, symbol, orderId);
                    OrderHistory order = new OrderHistory(binanceFuturesOrder, StartPrice);
                    Dispatcher.Invoke(new Action(() => {
                        list_orders.Add(order);
                        history_list_orders.Add(order);
                        LoadingChartOrders();
                    }));
                });
            }
            else if (orderId == long_order_id_0)
            {
                await Task.Run(() => {
                    BinanceFuturesOrder binanceFuturesOrder = InfoOrder(socket, symbol, orderId);
                    OrderHistory order = new OrderHistory(binanceFuturesOrder, StartPrice);
                    Dispatcher.Invoke(new Action(() => {
                        long_price_order_0 = order.AvgPrice;
                        NewLinesLong(Double.Parse(long_price_order_0.ToString()));
                        list_orders.Add(order);
                        history_list_orders.Add(order);
                        LoadingChartOrders();
                    }));
                });
            }
            else if (orderId == long_order_id_1)
            {
                await Task.Run(() => {
                    BinanceFuturesOrder binanceFuturesOrder = InfoOrder(socket, symbol, orderId);
                    OrderHistory order = new OrderHistory(binanceFuturesOrder, StartPrice);
                    Dispatcher.Invoke(new Action(() => {
                        long_price_order_1 = order.AvgPrice;
                        decimal average = Math.Round(((long_quantity_0 * long_price_order_0) + (long_quantity_1 * long_price_order_1)) / (long_quantity_1 + long_quantity_0), 6);
                        NewLineSLLong(Decimal.ToDouble(average));
                        list_orders.Add(order);
                        history_list_orders.Add(order);
                        LoadingChartOrders();
                    }));
                });
            }
            else if (orderId == long_order_id_2)
            {
                await Task.Run(() => {
                    BinanceFuturesOrder binanceFuturesOrder = InfoOrder(socket, symbol, orderId);
                    OrderHistory order = new OrderHistory(binanceFuturesOrder, StartPrice);
                    Dispatcher.Invoke(new Action(() => {
                        long_price_order_2 = order.AvgPrice;
                        decimal average = Math.Round(((long_quantity_0 * long_price_order_0) + (long_quantity_1 * long_price_order_1) + (long_quantity_2 * long_price_order_2)) / (long_quantity_1 + long_quantity_2 + long_quantity_0), 6);
                        NewLineSLLong(Decimal.ToDouble(average));
                        list_orders.Add(order);
                        history_list_orders.Add(order);
                        LoadingChartOrders();
                    }));
                });
            }
            else if (orderId == long_order_id_3)
            {
                await Task.Run(() => {
                    BinanceFuturesOrder binanceFuturesOrder = InfoOrder(socket, symbol, orderId);
                    OrderHistory order = new OrderHistory(binanceFuturesOrder, StartPrice);
                    Dispatcher.Invoke(new Action(() => {
                        long_price_order_3 = order.AvgPrice;
                        decimal average = Math.Round(((long_quantity_0 * long_price_order_0) + (long_quantity_1 * long_price_order_1) + (long_quantity_2 * long_price_order_2) + (long_quantity_3 * long_price_order_3)) / (long_quantity_1 + long_quantity_2 + long_quantity_3 + long_quantity_0), 6);
                        NewLineSLLong(Decimal.ToDouble(average));
                        list_orders.Add(order);
                        history_list_orders.Add(order);
                        LoadingChartOrders();
                    }));
                });
            }
            else if (orderId == long_close_order_id)
            {
                long_close_order_id = 0;
                long_price_order_0 = Decimal.Parse(line_sl_long_y[0].ToString());
                NewLinesLong(line_sl_long_y[0]);
                NewLineSLLongClear();
                await Task.Run(() => {
                    BinanceFuturesOrder binanceFuturesOrder = InfoOrder(socket, symbol, orderId);
                    OrderHistory order = new OrderHistory(binanceFuturesOrder, StartPrice);
                    Dispatcher.Invoke(new Action(() => {
                        list_orders.Add(order);
                        history_list_orders.Add(order);
                        LoadingChartOrders();
                    }));
                });
            }
        }
        public decimal InfoOrderId(Socket socket, string symbol, long orderId)
        {
            var result = socket.futures.Trading.GetOrdersAsync(symbol: symbol, orderId: orderId).Result;
            if (!result.Success)
            {
                Error($"InfoOrderId: {result.Error.Message}");
                return InfoOrderId(socket, symbol, orderId);
            }
            else
            {
                foreach (var it in result.Data.ToList())
                {
                    if (it.AvgPrice > 0m) return it.AvgPrice;
                }
                return InfoOrderId(socket, symbol, orderId);
            }
        }

        public async void OrderToList(Socket socket, string symbol, long orderId, decimal StartPrice)
        {
            await Task.Run(()=> {
                BinanceFuturesOrder binanceFuturesOrder = InfoOrder(socket, symbol, orderId);
                OrderHistory order = new OrderHistory(binanceFuturesOrder, StartPrice);
                Dispatcher.Invoke(new Action(()=> {
                    list_orders.Add(order);
                    history_list_orders.Add(order);
                    LoadingChartOrders();
                }));
            });
        }
        public BinanceFuturesOrder InfoOrder(Socket socket, string symbol, long orderId)
        {
            var result = socket.futures.Trading.GetOrdersAsync(symbol: symbol, orderId: orderId).Result;
            if (!result.Success)
            {
                Error($"InfoOrderId: {result.Error.Message}");
                return InfoOrder(socket, symbol, orderId);
            }
            else
            {
                foreach (var it in result.Data.ToList())
                {
                    if (it.AvgPrice > 0m) return it;
                }
                return InfoOrder(socket, symbol, orderId);
            }
        }

        #endregion

        #region - Calculate Pnl -
        //private async void Pnl(decimal price)
        //{
        //    await Task.Run(()=> { CalculatePnl(price); });
        //}
        private void CalculatePnl(decimal price)
        {
            try 
            {
                decimal sum = 0m;
                foreach (var it in list_orders)
                {
                    sum += it.RealizedProfit;
                    sum -= it.Commission;
                }
                if (short_order_id_0 != 0)
                {
                    sum += ((short_quantity_0 * short_price_order_0) - (short_quantity_0 * price));
                }
                if (short_order_id_1 != 0)
                {
                    sum += ((short_quantity_1 * short_price_order_1) - (short_quantity_1 * price));
                }
                if (short_order_id_2 != 0)
                {
                    sum += ((short_quantity_2 * short_price_order_2) - (short_quantity_2 * price));
                }
                if (short_order_id_3 != 0)
                {
                    sum += ((short_quantity_3 * short_price_order_3) - (short_quantity_3 * price));
                }
                if (long_order_id_0 != 0)
                {
                    sum += ((long_quantity_0 * price) - (long_quantity_0 * long_price_order_0));
                }
                if (long_order_id_1 != 0)
                {
                    sum += ((long_quantity_1 * price) - (long_quantity_1 * long_price_order_1));
                }
                if (long_order_id_2 != 0)
                {
                    sum += ((long_quantity_2 * price) - (long_quantity_2 * long_price_order_2));
                }
                if (long_order_id_3 != 0)
                {
                    sum += ((long_quantity_3 * price) - (long_quantity_3 * long_price_order_3));
                }
                variables.PNL = sum;
            } 
            catch(Exception ex)
            {
                Error($"CalculatePnl {ex.Message}");
            }
        }
        #endregion

        #region - Event Text Changed -
        private void COUNT_CANDLES_TextChanged(object sender, TextChangedEventArgs e)
        {
            ReloadChart();
        }

        private void INTERVAL_TIME_DropDownClosed(object sender, EventArgs e)
        {
            int index = INTERVAL_TIME.SelectedIndex;
            interval_time = IntervalCandles.Intervals()[index].interval;
            timeSpan = new TimeSpan(IntervalCandles.Intervals()[index].timespan);
            ReloadChart();
        }
        #endregion

        #region - Chart Line Take Profit -
        private void LINE_TP_TextChanged(object sender, TextChangedEventArgs e)
        {
            NewLineTP();
            plt.Refresh();
        }
        double[] line_tp_1_y = new double[2];
        double[] line_tp_2_y = new double[2];
        private void NewLineTP()
        {
            try
            {
                if (list_ohlc.Count > 0)
                {
                    Array.Clear(line_tp_1_y, 0, 2);
                    Array.Clear(line_tp_2_y, 0, 2);
                    plt.Plot.Remove(line_tp_1_scatter);
                    plt.Plot.Remove(line_tp_2_scatter);
                    if (LINE_TP == 0)
                    {
                        line_tp_1_y[0] = price_short;
                        line_tp_2_y[0] = price_short;
                    }
                    else
                    {
                        line_tp_1_y[0] = price_short + (price_short / 1000 * LINE_TP);
                        line_tp_2_y[0] = price_short + (price_short / 1000 * -LINE_TP);
                    }
                    line_tp_1_y[1] = line_tp_1_y[0];
                    line_tp_2_y[1] = line_tp_2_y[0];
                    line_tp_1_scatter = plt.Plot.AddScatterLines(line_x, line_tp_1_y, Color.Orange, lineStyle: LineStyle.Dash, label: $"{line_tp_1_y[0]} ({Convert.ToDouble(LINE_TP) / 10} %) - 1 take profit price");
                    line_tp_1_scatter.YAxisIndex = 1;
                    line_tp_2_scatter = plt.Plot.AddScatterLines(line_x, line_tp_2_y, Color.Orange, lineStyle: LineStyle.Dash, label: $"{line_tp_2_y[0]} (-{Convert.ToDouble(LINE_TP) / 10} %) - 2 take profit price");
                    line_tp_2_scatter.YAxisIndex = 1;
                }
            }
            catch (Exception c)
            {
                Error($"NewLineTP {c.Message}");
            }
        }
        #endregion

        #region - Chart Line Stop Loss  -
        double[] line_sl_short_y = new double[2];
        private void NewLineSLShort(double price_sl)
        {
            if (LINE_SL != 0 && list_ohlc.Count > 0)
            {
                try
                {
                    Array.Clear(line_sl_short_y, 0, 2);
                    plt.Plot.Remove(line_sl_short_scatter);
                    line_sl_short_y[0] = price_sl;
                    line_sl_short_y[1] = line_sl_short_y[0];
                    line_sl_short_scatter = plt.Plot.AddScatterLines(line_x, line_sl_short_y, Color.Red, lineStyle: LineStyle.Dash, label: line_sl_short_y[0] + " - stop loss short price");
                    line_sl_short_scatter.YAxisIndex = 1;
                    plt.Refresh();
                }
                catch (Exception c)
                {
                    Error($"NewLineSLShort {c.Message}");
                }
            }
        }
        private void NewLineSLShortClear()
        {
            plt.Plot.Remove(line_sl_short_scatter);
            plt.Refresh();
            Array.Clear(line_sl_short_y, 0, 2);
        }
        double[] line_sl_long_y = new double[2];
        private void NewLineSLLong(double price_sl)
        {
            if (LINE_SL != 0 && list_ohlc.Count > 0)
            {
                try
                {
                    Array.Clear(line_sl_long_y, 0, 2);
                    plt.Plot.Remove(line_sl_long_scatter);
                    line_sl_long_y[0] = price_sl;
                    line_sl_long_y[1] = line_sl_long_y[0];
                    line_sl_long_scatter = plt.Plot.AddScatterLines(line_x, line_sl_long_y, Color.Red, lineStyle: LineStyle.Dash, label: line_sl_long_y[0] + " - stop loss long price");
                    line_sl_long_scatter.YAxisIndex = 1;
                    plt.Refresh();
                }
                catch (Exception c)
                {
                    Error($"NewLineSLLong {c.Message}");
                }
            }
        }
        private void NewLineSLLongClear()
        {
            plt.Plot.Remove(line_sl_long_scatter);
            plt.Refresh();
            Array.Clear(line_sl_long_y, 0, 2);
        }
        #endregion

        #region - Chart Line Open Order  -
        private void LINE_OPEN_TextChanged(object sender, TextChangedEventArgs e)
        {
            if(variables.LINE_OPEN > 0)
            {
                NewLinesOpenShortOrders();
                NewLinesOpenLongOrders();
                plt.Refresh();
            }
        }
        private void NewLines(double price_order)
        {
            NewLineOpenShort(price_order);
            NewLineOpenLong(price_order);
            NewLinesOpenShortOrders(); 
            NewLinesOpenLongOrders();
            NewLineTP();
            plt.Refresh();
        }
        private void NewLinesShort(double price_order)
        {
            NewLineOpenShort(price_order);
            NewLinesOpenShortOrders();
            plt.Refresh();
        }
        private void NewLinesLong(double price_order)
        {
            NewLineOpenLong(price_order);
            NewLinesOpenLongOrders();
            plt.Refresh();
        }
        double price_short;
        double price_percent_short;
        double[] line_x = new double[2];
        double[] line_open_y_short = new double[2];
        double[] line_open_1_y_short = new double[2];
        double[] line_open_2_y_short = new double[2];
        double[] line_open_3_y_short = new double[2];
        private void NewLineOpenShort(double price_order)
        {
            try
            {
                if (variables.LINE_OPEN > 0 && list_ohlc.Count > 0)
                {
                    Array.Clear(line_x, 0, 2);
                    Array.Clear(line_open_y_short, 0, 2);
                    plt.Plot.Remove(line_open_short_scatter);
                    if (price_order == 0) price_short = list_ohlc[list_ohlc.Count - 1].Close;
                    else price_short = price_order;
                    line_x[0] = list_ohlc[0].DateTime.ToOADate();
                    line_x[1] = list_ohlc[list_ohlc.Count - 1].DateTime.ToOADate();
                    line_open_y_short[0] = price_short;
                    line_open_y_short[1] = price_short;
                    line_open_short_scatter = plt.Plot.AddScatterLines(line_x, line_open_y_short, Color.Pink, lineStyle: LineStyle.Dash, label: price_short + " - open order short price");
                    line_open_short_scatter.YAxisIndex = 1;
                }
            }
            catch (Exception c)
            {
                Error($"NewLineOpen {c.Message}");
            }
        }
        double price_long;
        double price_percent_long;
        double[] line_open_y_long = new double[2];
        double[] line_open_1_y_long = new double[2];
        double[] line_open_2_y_long = new double[2];
        double[] line_open_3_y_long = new double[2];
        private void NewLineOpenLong(double price_order)
        {
            try
            {
                if (variables.LINE_OPEN > 0 && list_ohlc.Count > 0)
                {
                    Array.Clear(line_x, 0, 2);
                    Array.Clear(line_open_y_long, 0, 2);
                    plt.Plot.Remove(line_open_long_scatter);
                    if (price_order == 0) price_long = list_ohlc[list_ohlc.Count - 1].Close;
                    else price_long = price_order;
                    line_x[0] = list_ohlc[0].DateTime.ToOADate();
                    line_x[1] = list_ohlc[list_ohlc.Count - 1].DateTime.ToOADate();
                    line_open_y_long[0] = price_long;
                    line_open_y_long[1] = price_long;
                    line_open_long_scatter = plt.Plot.AddScatterLines(line_x, line_open_y_long, Color.LightGreen, lineStyle: LineStyle.Dash, label: price_long + " - open order long price");
                    line_open_long_scatter.YAxisIndex = 1;
                }
            }
            catch (Exception c)
            {
                Error($"NewLineOpen {c.Message}");
            }
        }
        private void LoadLineOpen()
        {
            line_x[1] = list_ohlc[list_ohlc.Count - 1].DateTime.ToOADate();
        }

        private void NewLinesOpenShortOrders()
        {
            try
            {
                if (variables.LINE_OPEN != 0 && list_ohlc.Count > 0)
                {
                    Array.Clear(line_open_1_y_short, 0, 2);
                    Array.Clear(line_open_2_y_short, 0, 2);
                    Array.Clear(line_open_3_y_short, 0, 2);
                    plt.Plot.Remove(line_open_1_short_scatter);
                    plt.Plot.Remove(line_open_2_short_scatter);
                    plt.Plot.Remove(line_open_3_short_scatter);
                    price_percent_short = price_short / 1000 * variables.LINE_OPEN;
                    line_open_1_y_short[0] = price_short + price_percent_short;
                    line_open_1_y_short[1] = line_open_1_y_short[0];
                    line_open_2_y_short[0] = price_short + price_percent_short + price_percent_short;
                    line_open_2_y_short[1] = line_open_2_y_short[0];
                    line_open_3_y_short[0] = price_short + price_percent_short + price_percent_short + price_percent_short;
                    line_open_3_y_short[1] = line_open_3_y_short[0];
                    line_open_1_short_scatter = plt.Plot.AddScatterLines(line_x, line_open_1_y_short, Color.Pink, lineStyle: LineStyle.Dash, label: $"{line_open_1_y_short[0]} ({Convert.ToDouble(variables.LINE_OPEN) / 10} %) - 1 open order short price");
                    line_open_1_short_scatter.YAxisIndex = 1;
                    line_open_2_short_scatter = plt.Plot.AddScatterLines(line_x, line_open_2_y_short, Color.Pink, lineStyle: LineStyle.Dash, label: $"{line_open_2_y_short[0]} ({Convert.ToDouble(variables.LINE_OPEN) * 2 / 10} %) - 2 open order short price");
                    line_open_2_short_scatter.YAxisIndex = 1;
                    line_open_3_short_scatter = plt.Plot.AddScatterLines(line_x, line_open_3_y_short, Color.Pink, lineStyle: LineStyle.Dash, label: $"{line_open_3_y_short[0]} ({Convert.ToDouble(variables.LINE_OPEN) * 3 / 10} %) - 3 open order short price");
                    line_open_3_short_scatter.YAxisIndex = 1;
                }
            }
            catch (Exception c)
            {
                Error($"NewLinesOpenOrders {c.Message}");
            }
        }
        private void NewLinesOpenLongOrders()
        {
            try
            {
                if (variables.LINE_OPEN != 0 && list_ohlc.Count > 0)
                {
                    Array.Clear(line_open_1_y_long, 0, 2);
                    Array.Clear(line_open_2_y_long, 0, 2);
                    Array.Clear(line_open_3_y_long, 0, 2);
                    plt.Plot.Remove(line_open_1_long_scatter);
                    plt.Plot.Remove(line_open_2_long_scatter);
                    plt.Plot.Remove(line_open_3_long_scatter);
                    price_percent_long = - price_long / 1000 * variables.LINE_OPEN;
                    line_open_1_y_long[0] = price_long + price_percent_long;
                    line_open_1_y_long[1] = line_open_1_y_long[0];
                    line_open_2_y_long[0] = price_long + price_percent_long + price_percent_long;
                    line_open_2_y_long[1] = line_open_2_y_long[0];
                    line_open_3_y_long[0] = price_long + price_percent_long + price_percent_long + price_percent_long;
                    line_open_3_y_long[1] = line_open_3_y_long[0];
                    line_open_1_long_scatter = plt.Plot.AddScatterLines(line_x, line_open_1_y_long, Color.LightGreen, lineStyle: LineStyle.Dash, label: $"{line_open_1_y_long[0]} ({Convert.ToDouble(variables.LINE_OPEN) / 10} %) - 1 open order long price");
                    line_open_1_long_scatter.YAxisIndex = 1;
                    line_open_2_long_scatter = plt.Plot.AddScatterLines(line_x, line_open_2_y_long, Color.LightGreen, lineStyle: LineStyle.Dash, label: $"{line_open_2_y_long[0]} ({Convert.ToDouble(variables.LINE_OPEN) * 2 / 10} %) - 2 open order long price");
                    line_open_2_long_scatter.YAxisIndex = 1;
                    line_open_3_long_scatter = plt.Plot.AddScatterLines(line_x, line_open_3_y_long, Color.LightGreen, lineStyle: LineStyle.Dash, label: $"{line_open_3_y_long[0]} ({Convert.ToDouble(variables.LINE_OPEN) * 3 / 10} %) - 3 open order long price");
                    line_open_3_long_scatter.YAxisIndex = 1;
                }
            }
            catch (Exception c)
            {
                Error($"NewLinesOpenOrders {c.Message}");
            }
        }
        #endregion

        #region - Load Chart -
        private void LIST_SYMBOLS_DropDownClosed(object sender, EventArgs e)
        {
            ErrorWatcherChange();
            ReloadChart();
        }
        
        private void ReloadChart()
        {
            try
            {
                if (socket != null && COUNT_CANDLES > 0 && COUNT_CANDLES < 500)
                {
                    UnsubscribeAllAsync();
                    LoadingCandlesToDB();
                    if (variables.ONLINE_CHART)
                    {
                        BalanceFutureAsync();
                        SubscribeToOrderThread();
                        SubscribeToKline();
                        if (!timer.IsEnabled) timer.Start();
                    }
                    else { if (timer.IsEnabled) timer.Stop(); UnsubscribeAllAsync(); variables.CONNECT_BINANCE = false; }
                    NewLines(0);
                    LoadingChart();
                    ReloadSettings();
                    ClearListOrders();
                    LoadingChartOrders();
                    AverageCandleAsync();
                    plt.Plot.AxisAuto();
                    plt.Render();
                }
            }
            catch (Exception c)
            {
                Error($"ReloadChart {c.Message}");
            }
        }
        
        private void LoadingChart()
        {
            try
            {
                if (COUNT_CANDLES > 0 && list_ohlc.Count > 0)
                {
                    plt.Plot.Remove(candlePlot);
                    // Candles
                    candlePlot = plt.Plot.AddCandlesticks(list_ohlc.ToArray());
                    candlePlot.YAxisIndex = 1;
                    // Line open order
                    LoadLineOpen();
                    // Orders
                    ChartPointsOrders();

                    StartAlgorithm();
                }
            }
            catch (Exception c)
            {
                Error($"LoadingChart {c.Message}");
            }
        }
        #endregion

        #region - Load Candles -
        private void LoadingCandlesToDB()
        {
            try
            {
                string symbol = LIST_SYMBOLS.Text;
                if (symbol != "")
                {
                    Klines(symbol, klines_count: COUNT_CANDLES);
                }
            }
            catch (Exception c)
            {
                Error($"LoadingCandlesToDB {c.Message}");
            }
        }

        #endregion

        #region - Candles Save -
        public void Klines(string Symbol, int klines_count, DateTime? start_time = null, DateTime? end_time = null)
        {
            try
            {
                var result = socket.futures.ExchangeData.GetKlinesAsync(symbol: Symbol, interval: interval_time, startTime: start_time, endTime: end_time, limit: klines_count).Result;
                if (!result.Success) Error("Error GetKlinesAsync");
                else
                {
                    if (list_ohlc.Count > 0) list_ohlc.Clear();
                    foreach (var it in result.Data.ToList())
                    {
                        
                        list_ohlc.Add(new OHLC(Decimal.ToDouble(it.OpenPrice), Decimal.ToDouble(it.HighPrice), Decimal.ToDouble(it.LowPrice), Decimal.ToDouble(it.ClosePrice), it.OpenTime, timeSpan));
                    }
                    variables.PRICE_SYMBOL = result.Data.ToList()[result.Data.ToList().Count - 1].ClosePrice;
                    
                    ExchangeInfo();
                }
            }
            catch (Exception e)
            {
                Error($"Klines {e.Message}");
            }
        }

        #endregion

        #region - Sound Order-
        private void SoundOpenOrder()
        {
            try
            {
                if (variables.SOUND) new SoundPlayer(Properties.Resources.wav_2).Play();
            }
            catch (Exception c)
            {
                Error($"SoundOpenOrder {c.Message}");
            }
        }
        private void SoundCloseOrder()
        {
            try
            {
                if (variables.SOUND) new SoundPlayer(Properties.Resources.wav_1).Play();
            }
            catch (Exception c)
            {
                Error($"SoundCloseOrder {c.Message}");
            }
        }
        #endregion

        #region - Chart -
        private void Chart()
        {
            plt.Plot.Layout(padding: 12);
            plt.Plot.Style(figureBackground: Color.Black, dataBackground: Color.Black);
            plt.Plot.Frameless();
            plt.Plot.XAxis.TickLabelStyle(color: Color.White);
            plt.Plot.XAxis.TickMarkColor(ColorTranslator.FromHtml("#333333"));
            plt.Plot.XAxis.MajorGrid(color: ColorTranslator.FromHtml("#333333"));

            plt.Plot.YAxis.Ticks(false);
            plt.Plot.YAxis.Grid(false);
            plt.Plot.YAxis2.Ticks(true);
            plt.Plot.YAxis2.Grid(true);
            plt.Plot.YAxis2.TickLabelStyle(color: ColorTranslator.FromHtml("#00FF00"));
            plt.Plot.YAxis2.TickMarkColor(ColorTranslator.FromHtml("#333333"));
            plt.Plot.YAxis2.MajorGrid(color: ColorTranslator.FromHtml("#333333"));

            var legend = plt.Plot.Legend();
            legend.FillColor = Color.Transparent;
            legend.OutlineColor = Color.Transparent;
            legend.Font.Color = Color.White;
            legend.Font.Bold = true;
        }
        #endregion

        #region - List Sumbols -
        private void GetSumbolName()
        {
            foreach (var it in ListSymbols())
            {
                list_sumbols_name.Add(it.Symbol);
            }
            list_sumbols_name.Sort();
            LIST_SYMBOLS.Items.Refresh();
            LIST_SYMBOLS.SelectedIndex = 0;
        }
        public List<BinancePrice> ListSymbols()
        {
            try
            {
                var result = socket.futures.ExchangeData.GetPricesAsync().Result;
                if (!result.Success) Error("Error GetKlinesAsync");
                return result.Data.ToList();
            }
            catch (Exception e)
            {
                Error($"ListSymbols {e.Message}");
                return ListSymbols();
            }
        }

        #endregion

        #region - Login -
        private void Button_Save(object sender, RoutedEventArgs e)
        {
            try
            {
                if (CLIENT_NAME != "" && API_KEY != "" && SECRET_KEY != "")
                {
                    if (ConnectTrial.Check(CLIENT_NAME))
                    {
                        string path = System.IO.Path.Combine(Environment.CurrentDirectory, "clients");
                        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                        if (!File.Exists(path + "/" + CLIENT_NAME))
                        {

                            Client client = new Client(CLIENT_NAME, API_KEY, SECRET_KEY);
                            string json = JsonConvert.SerializeObject(client);
                            File.WriteAllText(path + "/" + CLIENT_NAME, json);
                            Clients();
                            CLIENT_NAME = "";
                            API_KEY = "";
                            SECRET_KEY = "";
                        }
                    }
                    else Error("Сlient name not found!");
                }
            }
            catch (Exception c)
            {
                Error($"Button_Save {c.Message}");
            }
        }
        private void Clients()
        {
            try
            {
                string path = System.IO.Path.Combine(Environment.CurrentDirectory, "clients");
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                List<string> filesDir = (from a in Directory.GetFiles(path) select System.IO.Path.GetFileNameWithoutExtension(a)).ToList();
                if (filesDir.Count > 0)
                {
                    ClientList file_list = new ClientList(filesDir);
                    BOX_NAME.ItemsSource = file_list.BoxNameContent;
                    BOX_NAME.SelectedItem = file_list.BoxNameContent[0];
                }
            }
            catch (Exception e)
            {
                Error($"Clients {e.Message}");
            }
        }
        private void Button_Login(object sender, RoutedEventArgs e)
        {
            try
            {
                if (API_KEY != "" && SECRET_KEY != "" && CLIENT_NAME != "")
                {
                    if (ConnectTrial.Check(CLIENT_NAME))
                    {
                        socket = new Socket(API_KEY, SECRET_KEY);
                        Login_Click();
                        CLIENT_NAME = "";
                        API_KEY = "";
                        SECRET_KEY = "";
                        Ping();
                    }
                    else Error("Сlient name not found!");
                }
                else if (BOX_NAME.Text != "")
                {
                    string path = System.IO.Path.Combine(Environment.CurrentDirectory, "clients");
                    string json = File.ReadAllText(path + "\\" + BOX_NAME.Text);
                    Client client = JsonConvert.DeserializeObject<Client>(json);
                    if (ConnectTrial.Check(client.ClientName))
                    {
                        socket = new Socket(client.ApiKey, client.SecretKey);
                        Login_Click();
                        Ping();
                    }
                    else Error("Сlient name not found!");
                }
            }
            catch (Exception c)
            {
                Error($"Button_Login {c.Message}");
            }
        }
        private void Login_Click()
        {
            LOGIN_GRID.Visibility = Visibility.Hidden;
            EXIT_GRID.Visibility = Visibility.Visible;
            GetSumbolName();
        }
        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            EXIT_GRID.Visibility = Visibility.Hidden;
            LOGIN_GRID.Visibility = Visibility.Visible;
            UnsubscribeAllAsync();
            socket = null;
            list_sumbols_name.Clear();
            variables.START_PING = false;
        }
        #endregion

        #region - Error -
        // ------------------------------------------------------- Start Error Text Block --------------------------------------
        private void ErrorWatcher()
        {
            try
            {
                error_watcher.Path = ErrorText.Directory();
                error_watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.LastAccess | NotifyFilters.FileName | NotifyFilters.DirectoryName;
                error_watcher.Changed += new FileSystemEventHandler(OnChanged);
                error_watcher.Filter = ErrorText.Patch();
                error_watcher.EnableRaisingEvents = true;
            }
            catch (Exception e)
            {
                Error($"ErrorWatcher {e.Message}");
            }
        }
        private void ErrorWatcherChange()
        {
            ErrorText.patch = LIST_SYMBOLS.Text;
            error_watcher.Filter = ErrorText.Patch();
        }
        private void OnChanged(object source, FileSystemEventArgs e)
        {
            Dispatcher.Invoke(new Action(() => { ERROR_LOG.Text = File.ReadAllText(ErrorText.FullPatch()); }));
        }
        private void Button_ClearErrors(object sender, RoutedEventArgs e)
        {
            File.WriteAllText(ErrorText.FullPatch(), "");
        }
        private async void Error(string error)
        {
            await Task.Run(() => { Dispatcher.BeginInvoke(new Action(() => { ErrorText.Add(error); })); }); 
        }
        // ------------------------------------------------------- End Error Text Block ----------------------------------------
        #endregion

    }
}
