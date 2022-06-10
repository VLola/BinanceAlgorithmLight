using Binance.Net.Enums;
using Binance.Net.Objects.Models.Futures;
using Binance.Net.Objects.Models.Futures.Socket;
using Binance.Net.Objects.Models.Spot;
using BinanceAlgorithmLight.Binance;
using BinanceAlgorithmLight.ConnectDB;
using BinanceAlgorithmLight.Errors;
using BinanceAlgorithmLight.Interval;
using BinanceAlgorithmLight.Model;
using Newtonsoft.Json;
using ScottPlot;
using ScottPlot.Plottable;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Color = System.Drawing.Color;

namespace BinanceAlgorithmLight
{
    public partial class MainWindow : Window
    {
        public FileSystemWatcher error_watcher = new FileSystemWatcher();
        public ErrorText ErrorText = new ErrorText(); 
        public List<BinanceFuturesStreamOrderUpdateData> history_list_orders = new List<BinanceFuturesStreamOrderUpdateData>();
        public List<BinanceFuturesStreamOrderUpdateData> list_orders = new List<BinanceFuturesStreamOrderUpdateData>();
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
        public ScatterPlot line_open_scatter;
        public ScatterPlot line_open_1_scatter;
        public ScatterPlot line_open_2_scatter;
        public ScatterPlot line_open_3_scatter;
        public ScatterPlot line_sl_1_scatter;
        public ScatterPlot line_tp_1_scatter;
        public ScatterPlot line_tp_2_scatter;
        public ScatterPlot order_long_open_plot;
        public ScatterPlot order_long_close_plot;
        public ScatterPlot order_short_open_plot;
        public ScatterPlot order_short_close_plot;
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        #region - Main Loaded -
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ErrorWatcher();
            Chart();
            Clients();
            INTERVAL_TIME.ItemsSource = IntervalCandles.Intervals();
            INTERVAL_TIME.SelectedIndex = 0;
            LIST_SYMBOLS.ItemsSource = list_sumbols_name;
            HISTORY_ORDER.ItemsSource = history_list_orders;
            EXIT_GRID.Visibility = Visibility.Hidden;
            LOGIN_GRID.Visibility = Visibility.Visible;
            this.DataContext = this;
        }
        #endregion

        #region - Trede History -
        private void TAB_CONTROL_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                decimal sum_total = 0m;
                int count_orders = 0;
                foreach (var it in history_list_orders)
                {
                    sum_total += it.RealizedProfit;
                    sum_total -= it.Fee;
                    count_orders++;
                }
                COUNT_ORDERS.Content = count_orders;
                SUM_TOTAL.Content = sum_total;
                if (sum_total > 0) SUM_TOTAL.Foreground = System.Windows.Media.Brushes.Green;
                else if (sum_total < 0) SUM_TOTAL.Foreground = System.Windows.Media.Brushes.Red;
                HISTORY_ORDER.Items.Refresh();
            }
            catch (Exception c)
            {
                ErrorText.Add($"TAB_CONTROL_MouseLeftButtonUp {c.Message}");
            }
        }
        #endregion

        #region - Average Candle -
        private void Average_Click(object sender, RoutedEventArgs e)
        {
            AverageCandle();
        }

        private void AverageCandle()
        {
            double sum_low_high_price = 0;
            foreach (var it in list_ohlc)
            {
                sum_low_high_price += (it.High - it.Low);
            }
            variables.AVERAGE_CANDLE = Math.Round((sum_low_high_price / (list_ohlc.Count - 1)) / (Decimal.ToDouble(variables.PRICE_SYMBOL) / 1000));
        }
        #endregion

        #region - Balance -
        public void BalanceFuture()
        {
            var result = socket.futures.Account.GetAccountInfoAsync().Result;
            if (!result.Success)
            {
                ErrorText.Add($"Failed BalanceFuture: {result.Error.Message}");
                BalanceFuture();
            }
            else
            {
                variables.ACCOUNT_BALANCE = result.Data.TotalMarginBalance;
            }
        }
        #endregion

        #region - Subscribe To Order -

        private void ClearListOrders()
        {
            if (list_orders.Count > 0) list_orders.Clear();
        }
        private void PriceOrder(BinanceFuturesStreamOrderUpdateData order)
        {
            if(order.PositionSide == PositionSide.Long && order.Side == OrderSide.Buy || order.PositionSide == PositionSide.Short && order.Side == OrderSide.Sell)
            {
                if (order.OrderId == open_order_id)
                {
                    price_open_order = order.AveragePrice;
                    NewLines(Double.Parse(price_open_order.ToString()));
                }
                else if (order.OrderId == order_id_1)
                {
                    price_order_1 = order.AveragePrice;
                    decimal average = Math.Round(((open_quantity * price_open_order) + (quantity_1 * price_order_1)) / (quantity_1 + open_quantity), 6);
                    NewLineSL(Decimal.ToDouble(average));
                    permission_to_close_orders = true;
                }
                else if (order.OrderId == order_id_2)
                {
                    price_order_2 = order.AveragePrice;
                    decimal average = Math.Round(((open_quantity * price_open_order) + (quantity_1 * price_order_1) + (quantity_2 * price_order_2)) / (quantity_1 + quantity_2 + open_quantity), 6);
                    NewLineSL(Decimal.ToDouble(average));
                    permission_to_close_orders = true;
                }
                else if (order.OrderId == order_id_3)
                {
                    price_order_3 = order.AveragePrice;
                    decimal average = Math.Round(((open_quantity * price_open_order) + (quantity_1 * price_order_1) + (quantity_2 * price_order_2) + (quantity_3 * price_order_3)) / (quantity_1 + quantity_2 + quantity_3 + open_quantity), 6);
                    NewLineSL(Decimal.ToDouble(average));
                    permission_to_close_orders = true;
                }
            }
            else if (order.OrderId == close_order_id)
            {
                close_order_id = 0;
                price_open_order = Decimal.Parse(line_sl_1_y[0].ToString());
                NewLines(line_sl_1_y[0]);
                NewLineSLClear();
            }
            
        }
        public void SubscribeToOrderThread()
        {
            try
            {
                string symbol = LIST_SYMBOLS.Text;
                new Thread(() => { SubscribeToOrder(symbol); }).Start();
            }
            catch (Exception c)
            {
                ErrorText.Add($"SubscribeToOrderThread {c.Message}");
            }
        }
        public async void SubscribeToOrder(string symbol)
        {
            try
            {
                bool unsubscribe = true;
                Dispatcher.Invoke(new Action(() =>
                {
                    ErrorText.Add($"Subscribe to orders {symbol}");
                }));
                var listenKey = await socket.futures.Account.StartUserStreamAsync();
                if (!listenKey.Success) {
                    Dispatcher.Invoke(new Action(() =>
                    {
                        ErrorText.Add($"Failed to start user stream: {listenKey.Error.Message}"); 
                        SubscribeToOrderThread();
                    }));
                    return;
                }
                var result = await socket.socketClient.UsdFuturesStreams.SubscribeToUserDataUpdatesAsync(listenKey: listenKey.Data,
                    onLeverageUpdate => { },
                    onMarginUpdate => { },
                    onAccountUpdate => {
                        Dispatcher.Invoke(new Action(() =>
                        {
                            variables.ACCOUNT_BALANCE = onAccountUpdate.Data.UpdateData.Balances.ToList()[0].CrossWalletBalance;
                        }));
                    },
                    onOrderUpdate =>
                    {
                        if (onOrderUpdate.Data.UpdateData.Symbol == symbol && onOrderUpdate.Data.UpdateData.Status == OrderStatus.Filled || onOrderUpdate.Data.UpdateData.Symbol == symbol && onOrderUpdate.Data.UpdateData.Status == OrderStatus.PartiallyFilled)
                        {
                            Dispatcher.Invoke(new Action(() =>
                            {
                                if (onOrderUpdate.Data.UpdateData.OrderId == open_order_id && !CheckOrderIdToListOrders() || onOrderUpdate.Data.UpdateData.OrderId == opposite_open_order_id && !CheckOrderIdToListOrders()) ClearListOrders();
                                list_orders.Add(onOrderUpdate.Data.UpdateData);
                                history_list_orders.Add(onOrderUpdate.Data.UpdateData);
                                PriceOrder(onOrderUpdate.Data.UpdateData);
                                LoadingChartOrders();
                            }));
                        }
                    },
                    onListenKeyExpired => {
                        if (unsubscribe)
                        {
                            Dispatcher.Invoke(new Action(() =>
                            {
                                ErrorText.Add($"Listen Key Expired {symbol}");
                                SubscribeToOrderThread();
                            }));
                        }
                        unsubscribe = false;
                    }
                    );
                if (!result.Success)
                {
                    Dispatcher.Invoke(new Action(() =>
                    {
                        ErrorText.Add($"Failed UserDataUpdates: {result.Error.Message}");
                        SubscribeToOrderThread();
                    }));
                }
            }
            catch (Exception c)
            {
                Dispatcher.Invoke(new Action(() =>
                {
                    ErrorText.Add($"SubscribeToOrder {c.Message}");
                    SubscribeToOrderThread();
                }));
            }
        }

        private bool CheckOrderIdToListOrders()
        {
            foreach(var it in list_orders)
            {
                if (it.OrderId == open_order_id || it.OrderId == opposite_open_order_id) return true;
            }
            return false;
        }
        #endregion

        #region - Subscribe To Kline -
        private void START_ASYNC_Click(object sender, RoutedEventArgs e)
        {
            SubscribeToKline();
            SubscribeToOrderThread();
        }
        private void STOP_ASYNC_Click(object sender, RoutedEventArgs e)
        {
            StopAsync();
        }
        private void StopAsync()
        {
            try
            {
                socket.socketClient.UnsubscribeAllAsync();
            }
            catch (Exception c)
            {
                ErrorText.Add($"STOP_ASYNC_Click {c.Message}");
            }
        }
        public void SubscribeToKline()
        {
            try
            {
                socket.socketClient.UsdFuturesStreams.SubscribeToKlineUpdatesAsync(LIST_SYMBOLS.Text, interval_time, Message =>
                {
                    Dispatcher.Invoke(new Action(() =>
                    {
                        variables.PRICE_SYMBOL = Message.Data.Data.ClosePrice;
                        UpdateListOHLC(new OHLC(Decimal.ToDouble(Message.Data.Data.OpenPrice), Decimal.ToDouble(Message.Data.Data.HighPrice), Decimal.ToDouble(Message.Data.Data.LowPrice), Decimal.ToDouble(Message.Data.Data.ClosePrice), Message.Data.Data.OpenTime, timeSpan));
                        LoadingChart();
                        plt.Render();
                        if (variables.EXPECTED_PNL_CHECK && variables.PNL > variables.EXPECTED_PNL && variables.RESTART_ALGORITHM)
                        {
                            variables.PNL = 0m;
                            CloseOrders();
                            variables.START_BET = true;
                            OpenOrders();
                        }
                        else if (variables.EXPECTED_PNL_CHECK && variables.PNL > variables.EXPECTED_PNL)
                        {
                            variables.PNL = 0m;
                            CloseOrders();
                        }
                    }));
                });
            }
            catch (Exception c)
            {
                ErrorText.Add($"STOP_ASYNC_Click {c.Message}");
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
                if (!result.Result.Success) ErrorText.Add("Error ExchangeInfo");
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
                ErrorText.Add($"ExchangeInfo {c.Message}");
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
                    long_open_order_y.Add(Double.Parse(it.AveragePrice.ToString()));
                }
                if (it.PositionSide == PositionSide.Long && it.Side == OrderSide.Sell)
                {
                    long_close_order_x.Add(it.UpdateTime.ToOADate());
                    long_close_order_y.Add(Double.Parse(it.AveragePrice.ToString()));
                }
                if (it.PositionSide == PositionSide.Short && it.Side == OrderSide.Sell)
                {
                    short_open_order_x.Add(it.UpdateTime.ToOADate());
                    short_open_order_y.Add(Double.Parse(it.AveragePrice.ToString()));
                }
                if (it.PositionSide == PositionSide.Short && it.Side == OrderSide.Buy)
                {
                    short_close_order_x.Add(it.UpdateTime.ToOADate());
                    short_close_order_y.Add(Double.Parse(it.AveragePrice.ToString()));
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

        public decimal open_quantity;
        public long open_order_id = 0;
        public decimal price_open_order;
        public decimal opposite_open_quantity;
        public long opposite_open_order_id = 0;
        private void OpenOrders_Click(object sender, RoutedEventArgs e)
        {
            OpenOrders();
        }
        private void OpenOrders()
        {
            try
            {
                string symbol = LIST_SYMBOLS.Text;
                if (variables.START_BET && variables.PRICE_SYMBOL > 0m && variables.LINE_OPEN < 0 && open_order_id == 0 && variables.LONG)
                {

                    open_quantity = RoundQuantity(variables.USDT_BET * 2 / variables.PRICE_SYMBOL);
                    open_order_id = Algorithm.Algorithm.Order(socket, symbol, OrderSide.Sell, FuturesOrderType.Market, open_quantity, PositionSide.Short);
                    opposite_open_quantity = RoundQuantity(open_quantity * 2);
                    opposite_open_order_id = Algorithm.Algorithm.Order(socket, symbol, OrderSide.Buy, FuturesOrderType.Market, opposite_open_quantity, PositionSide.Long);
                    if (open_order_id != 0 && opposite_open_order_id != 0)
                    {
                        start = true;
                    }
                    SoundOpenOrder();
                }
                else if (variables.START_BET && variables.PRICE_SYMBOL > 0m && variables.LINE_OPEN > 0 && open_order_id == 0 && variables.SHORT)
                {
                    open_quantity = RoundQuantity(variables.USDT_BET * 2 / variables.PRICE_SYMBOL);
                    open_order_id = Algorithm.Algorithm.Order(socket, symbol, OrderSide.Buy, FuturesOrderType.Market, open_quantity, PositionSide.Long);
                    opposite_open_quantity = RoundQuantity(open_quantity * 2);
                    opposite_open_order_id = Algorithm.Algorithm.Order(socket, symbol, OrderSide.Sell, FuturesOrderType.Market, opposite_open_quantity, PositionSide.Short);
                    if (open_order_id != 0 && opposite_open_order_id != 0)
                    {
                        start = true;
                    }
                    SoundOpenOrder();
                }
            }
            catch (Exception c)
            {
                ErrorText.Add($"OpenOrders {c.Message}");
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
                if (variables.LONG && open_order_id != 0)
                {
                    Algorithm.Algorithm.Order(socket, symbol, OrderSide.Buy, FuturesOrderType.Market, open_quantity, PositionSide.Short);
                    open_order_id = 0;
                    open_quantity = 0m;
                    SoundCloseOrder();
                }
                if (variables.LONG && opposite_open_order_id != 0)
                {
                    Algorithm.Algorithm.Order(socket, symbol, OrderSide.Sell, FuturesOrderType.Market, opposite_open_quantity, PositionSide.Long);
                    opposite_open_order_id = 0;
                    opposite_open_quantity = 0m;
                    SoundCloseOrder();
                }
                if (variables.LONG && order_id_1 != 0)
                {
                    Algorithm.Algorithm.Order(socket, symbol, OrderSide.Buy, FuturesOrderType.Market, quantity_1, PositionSide.Short);
                    order_id_1 = 0;
                    quantity_1 = 0m;
                    SoundCloseOrder();
                }
                if (variables.LONG && order_id_2 != 0)
                {
                    Algorithm.Algorithm.Order(socket, symbol, OrderSide.Buy, FuturesOrderType.Market, quantity_2, PositionSide.Short);
                    order_id_2 = 0;
                    quantity_2 = 0m;
                    SoundCloseOrder();
                }
                if (variables.LONG && order_id_3 != 0)
                {
                    Algorithm.Algorithm.Order(socket, symbol, OrderSide.Buy, FuturesOrderType.Market, quantity_3, PositionSide.Short);
                    order_id_3 = 0;
                    quantity_3 = 0m;
                    SoundCloseOrder();
                }
                if (variables.SHORT && open_order_id != 0)
                {
                    Algorithm.Algorithm.Order(socket, symbol, OrderSide.Sell, FuturesOrderType.Market, open_quantity, PositionSide.Long);
                    open_order_id = 0;
                    open_quantity = 0m;
                    SoundCloseOrder();
                }
                if (variables.SHORT && opposite_open_order_id != 0)
                {
                    Algorithm.Algorithm.Order(socket, symbol, OrderSide.Buy, FuturesOrderType.Market, opposite_open_quantity, PositionSide.Short);
                    opposite_open_order_id = 0;
                    opposite_open_quantity = 0m;
                    SoundCloseOrder();
                }
                if (variables.SHORT && order_id_1 != 0)
                {
                    Algorithm.Algorithm.Order(socket, symbol, OrderSide.Sell, FuturesOrderType.Market, quantity_1, PositionSide.Long);
                    order_id_1 = 0;
                    quantity_1 = 0m;
                    SoundCloseOrder();
                }
                if (variables.SHORT && order_id_2 != 0)
                {
                    Algorithm.Algorithm.Order(socket, symbol, OrderSide.Sell, FuturesOrderType.Market, quantity_2, PositionSide.Long);
                    order_id_2 = 0;
                    quantity_2 = 0m;
                    SoundCloseOrder();
                }
                if (variables.SHORT && order_id_3 != 0)
                {
                    Algorithm.Algorithm.Order(socket, symbol, OrderSide.Sell, FuturesOrderType.Market, quantity_3, PositionSide.Long);
                    order_id_3 = 0;
                    quantity_3 = 0m;
                    SoundCloseOrder();
                }
                NewLineSLClear();
                variables.PNL = CalculatePnl(variables.PRICE_SYMBOL);
                ReloadSettings();
            }
            catch (Exception c)
            {
                ErrorText.Add($"CloseOrders {c.Message}");
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
            open_order_id = 0;
            opposite_open_order_id = 0;
            order_id_1 = 0;
            order_id_2 = 0;
            order_id_3 = 0;
            close_order_id = 0;
            open_quantity = 0m;
            opposite_open_quantity = 0m;
            quantity_1 = 0m;
            quantity_2 = 0m;
            quantity_3 = 0m;
            start = false;
            variables.START_BET = false;
        }
        public decimal quantity_1 = 0m;
        public decimal quantity_2 = 0m;
        public decimal quantity_3 = 0m;
        public decimal price_order_1;
        public decimal price_order_2;
        public decimal price_order_3;
        public long order_id_1 = 0;
        public long order_id_2 = 0;
        public long order_id_3 = 0;
        public long close_order_id = 0;
        public bool start = false;
        public bool permission_to_close_orders = false;
        private void StartAlgorithm()
        {
            try
            {
                string symbol = LIST_SYMBOLS.Text;
                if (variables.ONLINE_CHART && variables.START_BET && start)
                {
                    // Short
                    if (variables.SHORT && order_id_1 == 0 && list_ohlc[list_ohlc.Count - 1].Close > line_open_1_y[0])
                    {
                        quantity_1 = RoundQuantity(open_quantity * 0.75m);
                        order_id_1 = Algorithm.Algorithm.Order(socket, symbol, OrderSide.Buy, FuturesOrderType.Market, quantity_1, PositionSide.Long);
                        SoundOpenOrder();
                    }
                    if (variables.SHORT && order_id_2 == 0 && list_ohlc[list_ohlc.Count - 1].Close > line_open_2_y[0])
                    {
                        quantity_2 = RoundQuantity(open_quantity * 0.6m);
                        order_id_2 = Algorithm.Algorithm.Order(socket, symbol, OrderSide.Buy, FuturesOrderType.Market, quantity_2, PositionSide.Long);
                        SoundOpenOrder();
                    }
                    if (variables.SHORT && order_id_3 == 0 && list_ohlc[list_ohlc.Count - 1].Close > line_open_3_y[0])
                    {
                        quantity_3 = RoundQuantity(open_quantity * 0.5m);
                        order_id_3 = Algorithm.Algorithm.Order(socket, symbol, OrderSide.Buy, FuturesOrderType.Market, quantity_3, PositionSide.Long);
                        SoundOpenOrder();
                    }
                    if (variables.SHORT && list_ohlc[list_ohlc.Count - 1].Close < line_sl_1_y[0] && permission_to_close_orders)
                    {
                        if (order_id_1 != 0 && order_id_2 != 0 && order_id_3 != 0)
                        {
                            close_order_id = Algorithm.Algorithm.Order(socket, symbol, OrderSide.Sell, FuturesOrderType.Market, quantity_1 + quantity_2 + quantity_3, PositionSide.Long);
                            quantity_1 = 0m;
                            order_id_1 = 0;
                            quantity_2 = 0m;
                            order_id_2 = 0;
                            quantity_3 = 0m;
                            order_id_3 = 0;
                            SoundCloseOrder();
                        }
                        else if (order_id_1 != 0 && order_id_2 != 0)
                        {
                            close_order_id = Algorithm.Algorithm.Order(socket, symbol, OrderSide.Sell, FuturesOrderType.Market, quantity_1 + quantity_2, PositionSide.Long);
                            quantity_1 = 0m;
                            order_id_1 = 0;
                            quantity_2 = 0m;
                            order_id_2 = 0;
                            SoundCloseOrder();
                        }
                        else if (order_id_1 != 0)
                        {
                            close_order_id = Algorithm.Algorithm.Order(socket, symbol, OrderSide.Sell, FuturesOrderType.Market, quantity_1, PositionSide.Long);
                            quantity_1 = 0m;
                            order_id_1 = 0;
                            SoundCloseOrder();
                        }
                        permission_to_close_orders = false;
                    }

                    // Long
                    if (variables.LONG && order_id_1 == 0 && list_ohlc[list_ohlc.Count - 1].Close < line_open_1_y[0])
                    {
                        quantity_1 = RoundQuantity(open_quantity * 0.75m);
                        order_id_1 = Algorithm.Algorithm.Order(socket, symbol, OrderSide.Sell, FuturesOrderType.Market, quantity_1, PositionSide.Short);
                        SoundOpenOrder();
                    }
                    if (variables.LONG && order_id_2 == 0 && list_ohlc[list_ohlc.Count - 1].Close < line_open_2_y[0])
                    {
                        quantity_2 = RoundQuantity(open_quantity * 0.6m);
                        order_id_2 = Algorithm.Algorithm.Order(socket, symbol, OrderSide.Sell, FuturesOrderType.Market, quantity_2, PositionSide.Short);
                        SoundOpenOrder();
                    }
                    if (variables.LONG && order_id_3 == 0 && list_ohlc[list_ohlc.Count - 1].Close < line_open_3_y[0])
                    {
                        quantity_3 = RoundQuantity(open_quantity * 0.5m);
                        order_id_3 = Algorithm.Algorithm.Order(socket, symbol, OrderSide.Sell, FuturesOrderType.Market, quantity_3, PositionSide.Short);
                        SoundOpenOrder();
                    }
                    if (variables.LONG && list_ohlc[list_ohlc.Count - 1].Close > line_sl_1_y[0] && permission_to_close_orders)
                    {
                        if (order_id_1 != 0 && order_id_2 != 0 && order_id_3 != 0)
                        {
                            close_order_id = Algorithm.Algorithm.Order(socket, symbol, OrderSide.Buy, FuturesOrderType.Market, quantity_1 + quantity_2 + quantity_3, PositionSide.Short);
                            quantity_1 = 0m;
                            order_id_1 = 0;
                            quantity_2 = 0m;
                            order_id_2 = 0;
                            quantity_3 = 0m;
                            order_id_3 = 0;
                            SoundCloseOrder();
                        }
                        else if (order_id_1 != 0 && order_id_2 != 0)
                        {
                            close_order_id = Algorithm.Algorithm.Order(socket, symbol, OrderSide.Buy, FuturesOrderType.Market, quantity_1 + quantity_2, PositionSide.Short);
                            quantity_1 = 0m;
                            order_id_1 = 0;
                            quantity_2 = 0m;
                            order_id_2 = 0;
                            SoundCloseOrder();
                        }
                        else if (order_id_1 != 0)
                        {
                            close_order_id = Algorithm.Algorithm.Order(socket, symbol, OrderSide.Buy, FuturesOrderType.Market, quantity_1, PositionSide.Short);
                            quantity_1 = 0m;
                            order_id_1 = 0;
                            SoundCloseOrder();
                        }
                        permission_to_close_orders = false;
                    }


                    // Take profit
                    if (variables.SHORT && open_order_id != 0 && opposite_open_order_id != 0 && list_ohlc[list_ohlc.Count - 1].Close > line_tp_1_y[0])
                    {
                        Algorithm.Algorithm.Order(socket, symbol, OrderSide.Sell, FuturesOrderType.Market, quantity_1, PositionSide.Long);
                        Algorithm.Algorithm.Order(socket, symbol, OrderSide.Sell, FuturesOrderType.Market, quantity_2, PositionSide.Long);
                        Algorithm.Algorithm.Order(socket, symbol, OrderSide.Sell, FuturesOrderType.Market, quantity_3, PositionSide.Long);
                        Algorithm.Algorithm.Order(socket, symbol, OrderSide.Sell, FuturesOrderType.Market, open_quantity, PositionSide.Long);
                        Algorithm.Algorithm.Order(socket, symbol, OrderSide.Buy, FuturesOrderType.Market, opposite_open_quantity, PositionSide.Short);
                        open_quantity = 0m;
                        open_order_id = 0;
                        opposite_open_order_id = 0;
                        opposite_open_quantity = 0m;
                        quantity_1 = 0m;
                        order_id_1 = 0;
                        quantity_2 = 0m;
                        order_id_2 = 0;
                        quantity_3 = 0m;
                        order_id_3 = 0;
                        SoundCloseOrder();
                        NewLineSLClear();
                        variables.PNL = CalculatePnl(variables.PRICE_SYMBOL);
                        ReloadSettings();
                    }
                    if (variables.SHORT && open_order_id != 0 && opposite_open_order_id != 0 && list_ohlc[list_ohlc.Count - 1].Close < line_tp_2_y[0])
                    {
                        Algorithm.Algorithm.Order(socket, symbol, OrderSide.Sell, FuturesOrderType.Market, open_quantity, PositionSide.Long);
                        Algorithm.Algorithm.Order(socket, symbol, OrderSide.Buy, FuturesOrderType.Market, opposite_open_quantity, PositionSide.Short);
                        open_quantity = 0m;
                        open_order_id = 0;
                        opposite_open_order_id = 0;
                        opposite_open_quantity = 0m;
                        SoundCloseOrder();
                        NewLineSLClear();
                        variables.PNL = CalculatePnl(variables.PRICE_SYMBOL);
                        ReloadSettings();
                    }
                    if (variables.LONG && open_order_id != 0 && opposite_open_order_id != 0 && list_ohlc[list_ohlc.Count - 1].Close > line_tp_1_y[0])
                    {
                        Algorithm.Algorithm.Order(socket, symbol, OrderSide.Buy, FuturesOrderType.Market, open_quantity, PositionSide.Short);
                        Algorithm.Algorithm.Order(socket, symbol, OrderSide.Sell, FuturesOrderType.Market, opposite_open_quantity, PositionSide.Long);
                        open_quantity = 0m;
                        open_order_id = 0;
                        opposite_open_order_id = 0;
                        opposite_open_quantity = 0m;
                        SoundCloseOrder();
                        NewLineSLClear();
                        variables.PNL = CalculatePnl(variables.PRICE_SYMBOL);
                        ReloadSettings();
                    }
                    if (variables.LONG && open_order_id != 0 && opposite_open_order_id != 0 && list_ohlc[list_ohlc.Count - 1].Close < line_tp_2_y[0])
                    {
                        decimal quantity_sum = quantity_1 + quantity_2 + quantity_3;
                        Algorithm.Algorithm.Order(socket, symbol, OrderSide.Buy, FuturesOrderType.Market, quantity_1, PositionSide.Short);
                        Algorithm.Algorithm.Order(socket, symbol, OrderSide.Buy, FuturesOrderType.Market, quantity_2, PositionSide.Short);
                        Algorithm.Algorithm.Order(socket, symbol, OrderSide.Buy, FuturesOrderType.Market, quantity_3, PositionSide.Short);
                        Algorithm.Algorithm.Order(socket, symbol, OrderSide.Buy, FuturesOrderType.Market, open_quantity, PositionSide.Short);
                        Algorithm.Algorithm.Order(socket, symbol, OrderSide.Sell, FuturesOrderType.Market, opposite_open_quantity, PositionSide.Long);
                        open_quantity = 0m;
                        open_order_id = 0;
                        opposite_open_order_id = 0;
                        opposite_open_quantity = 0m;
                        quantity_1 = 0m;
                        order_id_1 = 0;
                        quantity_2 = 0m;
                        order_id_2 = 0;
                        quantity_3 = 0m;
                        order_id_3 = 0;
                        SoundCloseOrder();
                        NewLineSLClear(); 
                        variables.PNL = CalculatePnl(variables.PRICE_SYMBOL);
                        ReloadSettings();
                    }
                    variables.PNL = CalculatePnl(variables.PRICE_SYMBOL);
                }
            }
            catch (Exception c)
            {
                ErrorText.Add($"StartAlgorithm {c.Message}");
            }
        }

        #endregion

        #region - Calculate Pnl -
        private decimal CalculatePnl(decimal price)
        {
            decimal sum = 0m;
            foreach (var it in list_orders)
            {
                if (it.OrderId == open_order_id || it.OrderId == order_id_1 || it.OrderId == order_id_2 || it.OrderId == order_id_3 || it.OrderId == opposite_open_order_id)
                {
                    if (it.PositionSide == PositionSide.Short && it.Side == OrderSide.Sell)
                    {
                        sum += ((it.QuantityOfLastFilledTrade * it.AveragePrice) - (it.QuantityOfLastFilledTrade * price));
                        sum -= it.Fee;
                    }
                    else if (it.PositionSide == PositionSide.Long && it.Side == OrderSide.Buy)
                    {
                        sum += ((it.QuantityOfLastFilledTrade * price) - (it.QuantityOfLastFilledTrade * it.AveragePrice));
                        sum -= it.Fee;
                    }
                }
                else
                {
                    sum += it.RealizedProfit;
                    sum -= it.Fee;
                }
            }
            return sum;
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
                        line_tp_1_y[0] = price;
                        line_tp_2_y[0] = price;
                    }
                    else
                    {
                        line_tp_1_y[0] = price + (price / 1000 * LINE_TP);
                        line_tp_2_y[0] = price + (price / 1000 * -LINE_TP);
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
                ErrorText.Add($"NewLineTP {c.Message}");
            }
        }
        #endregion

        #region - Chart Line Stop Loss  -
        double[] line_sl_1_y = new double[2];
        private void NewLineSL(double price_sl)
        {
            if (LINE_SL != 0 && list_ohlc.Count > 0)
            {
                try
                {
                    Array.Clear(line_sl_1_y, 0, 2);
                    plt.Plot.Remove(line_sl_1_scatter);
                    line_sl_1_y[0] = price_sl;
                    line_sl_1_y[1] = line_sl_1_y[0];
                    line_sl_1_scatter = plt.Plot.AddScatterLines(line_x, line_sl_1_y, Color.Red, lineStyle: LineStyle.Dash, label: line_sl_1_y[0] + " - stop loss price");
                    line_sl_1_scatter.YAxisIndex = 1;
                    plt.Refresh();
                }
                catch (Exception c)
                {
                    ErrorText.Add($"NewLineSL {c.Message}");
                }
            }
        }
        private void NewLineSLClear()
        {
            plt.Plot.Remove(line_sl_1_scatter);
            plt.Refresh();
        }
        #endregion

        #region - Chart Line Open Order  -
        private void LINE_OPEN_TextChanged(object sender, TextChangedEventArgs e)
        {
            NewLinesOpenOrders();
            plt.Refresh();
        }
        private void NewLines(double price_order)
        {
            NewLineOpen(price_order);
            NewLinesOpenOrders();
            NewLineTP();
            plt.Refresh();
        }
        double price;
        double price_percent;
        double[] line_x = new double[2];
        double[] line_open_y = new double[2];
        double[] line_open_1_y = new double[2];
        double[] line_open_2_y = new double[2];
        double[] line_open_3_y = new double[2];
        private void NewLineOpen(double price_order)
        {
            try
            {
                if (variables.LINE_OPEN != 0 && list_ohlc.Count > 0)
                {
                    Array.Clear(line_x, 0, 2);
                    Array.Clear(line_open_y, 0, 2);
                    plt.Plot.Remove(line_open_scatter);
                    if (price_order == 0) price = list_ohlc[list_ohlc.Count - 1].Close;
                    else price = price_order;
                    line_x[0] = list_ohlc[0].DateTime.ToOADate();
                    line_x[1] = list_ohlc[list_ohlc.Count - 1].DateTime.ToOADate();
                    line_open_y[0] = price;
                    line_open_y[1] = price;
                    line_open_scatter = plt.Plot.AddScatterLines(line_x, line_open_y, Color.White, lineStyle: LineStyle.Dash, label: price + " - open order price");
                    line_open_scatter.YAxisIndex = 1;
                }
            }
            catch (Exception c)
            {
                ErrorText.Add($"NewLineOpen {c.Message}");
            }
        }
        private void LoadLineOpen()
        {
            line_x[1] = list_ohlc[list_ohlc.Count - 1].DateTime.ToOADate();
        }

        private void NewLinesOpenOrders()
        {
            try
            {
                if (variables.LINE_OPEN != 0 && list_ohlc.Count > 0)
                {
                    Array.Clear(line_open_1_y, 0, 2);
                    Array.Clear(line_open_2_y, 0, 2);
                    Array.Clear(line_open_3_y, 0, 2);
                    plt.Plot.Remove(line_open_1_scatter);
                    plt.Plot.Remove(line_open_2_scatter);
                    plt.Plot.Remove(line_open_3_scatter);
                    price_percent = price / 1000 * variables.LINE_OPEN;
                    line_open_1_y[0] = price + price_percent;
                    line_open_1_y[1] = line_open_1_y[0];
                    line_open_2_y[0] = price + price_percent + price_percent;
                    line_open_2_y[1] = line_open_2_y[0];
                    line_open_3_y[0] = price + price_percent + price_percent + price_percent;
                    line_open_3_y[1] = line_open_3_y[0];
                    line_open_1_scatter = plt.Plot.AddScatterLines(line_x, line_open_1_y, Color.LightGreen, lineStyle: LineStyle.Dash, label: $"{line_open_1_y[0]} ({Convert.ToDouble(variables.LINE_OPEN) / 10} %) - 1 open order price");
                    line_open_1_scatter.YAxisIndex = 1;
                    line_open_2_scatter = plt.Plot.AddScatterLines(line_x, line_open_2_y, Color.LightGreen, lineStyle: LineStyle.Dash, label: $"{line_open_2_y[0]} ({Convert.ToDouble(variables.LINE_OPEN) * 2 / 10} %) - 2 open order price");
                    line_open_2_scatter.YAxisIndex = 1;
                    line_open_3_scatter = plt.Plot.AddScatterLines(line_x, line_open_3_y, Color.LightGreen, lineStyle: LineStyle.Dash, label: $"{line_open_3_y[0]} ({Convert.ToDouble(variables.LINE_OPEN) * 3 / 10} %) - 3 open order price");
                    line_open_3_scatter.YAxisIndex = 1;
                }
            }
            catch (Exception c)
            {
                ErrorText.Add($"NewLinesOpenOrders {c.Message}");
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
                    StopAsync();
                    LoadingCandlesToDB();
                    if (variables.ONLINE_CHART) {
                        BalanceFuture();
                        SubscribeToOrderThread();
                        SubscribeToKline(); 
                    }
                    NewLines(0);
                    LoadingChart();
                    ReloadSettings();
                    ClearListOrders();
                    LoadingChartOrders();
                    AverageCandle();
                    plt.Plot.AxisAuto();
                    plt.Render();
                }
            }
            catch (Exception c)
            {
                ErrorText.Add($"ReloadChart {c.Message}");
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
                ErrorText.Add($"LoadingChart {c.Message}");
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
                ErrorText.Add($"LoadingCandlesToDB {c.Message}");
            }
        }

        #endregion

        #region - Candles Save -
        public void Klines(string Symbol, int klines_count, DateTime? start_time = null, DateTime? end_time = null)
        {
            try
            {
                var result = socket.futures.ExchangeData.GetKlinesAsync(symbol: Symbol, interval: interval_time, startTime: start_time, endTime: end_time, limit: klines_count).Result;
                if (!result.Success) ErrorText.Add("Error GetKlinesAsync");
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
                ErrorText.Add($"Klines {e.Message}");
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
                ErrorText.Add($"SoundOpenOrder {c.Message}");
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
                ErrorText.Add($"SoundCloseOrder {c.Message}");
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
                if (!result.Success) ErrorText.Add("Error GetKlinesAsync");
                return result.Data.ToList();
            }
            catch (Exception e)
            {
                ErrorText.Add($"ListSymbols {e.Message}");
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
                    else ErrorText.Add("Сlient name not found!");
                }
            }
            catch (Exception c)
            {
                ErrorText.Add($"Button_Save {c.Message}");
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
                ErrorText.Add($"Clients {e.Message}");
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
                    }
                    else ErrorText.Add("Сlient name not found!");
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
                    }
                    else ErrorText.Add("Сlient name not found!");
                }
            }
            catch (Exception c)
            {
                ErrorText.Add($"Button_Login {c.Message}");
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
            socket = null;
            list_sumbols_name.Clear();
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
                ErrorText.Add($"ErrorWatcher {e.Message}");
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
        // ------------------------------------------------------- End Error Text Block ----------------------------------------
        #endregion

    }
}
