using System;
using System.Linq;
using System.Threading;
using System.IO;
using ScottPlot;
using ScottPlot.Plottables;
using System.Threading.Tasks;

namespace Lab08
{
    internal class Program
    {
        public class SystemMetrics
        {
            public double IdleProbability { get; set; }
            public double RejectionProbability { get; set; }
            public double ThroughputRelative { get; set; }
            public double ThroughputAbsolute { get; set; }
            public double BusyChannelsAverage { get; set; }
        }

        static void CalculateAndCompareMetrics(double arrivalRate, double serviceRate, int maxThreads, string outputFile)
        {
            SystemMetrics theoryValues = new SystemMetrics();

            double trafficIntensity = arrivalRate / serviceRate;
            double denominatorSum = 0;
            for (int i = 0; i <= maxThreads; i++)
            {
                denominatorSum += Math.Pow(trafficIntensity, i) / ComputeFactorial(i);
            }
            theoryValues.IdleProbability = Math.Pow(denominatorSum, -1);
            theoryValues.RejectionProbability = Math.Pow(trafficIntensity, maxThreads) / ComputeFactorial(maxThreads) * theoryValues.IdleProbability;
            theoryValues.ThroughputRelative = 1 - theoryValues.RejectionProbability;
            theoryValues.ThroughputAbsolute = theoryValues.ThroughputRelative * arrivalRate;
            theoryValues.BusyChannelsAverage = theoryValues.ThroughputAbsolute / serviceRate;

            Server processingServer = new Server(1000 / serviceRate, maxThreads);
            processingServer.StartMonitoring();
            Client requestClient = new Client(processingServer);
            for (int requestId = 1; requestId <= 100; requestId++)
            {
                requestClient.send(requestId);
                Thread.Sleep((int)(1000 / arrivalRate));
            }

            processingServer.StopMonitoring();

            SystemMetrics actualValues = new SystemMetrics();

            actualValues.IdleProbability = (double)processingServer.IdleChecks / processingServer.TotalChecks;
            actualValues.RejectionProbability = (double)processingServer.rejectedCount / processingServer.requestCount;
            actualValues.ThroughputRelative = (double)processingServer.processedCount / processingServer.requestCount;
            actualValues.ThroughputAbsolute = arrivalRate * (double)processingServer.processedCount / processingServer.requestCount;
            actualValues.BusyChannelsAverage = (double)processingServer.TotalOccupiedThreads / processingServer.TotalChecks;

            Console.WriteLine("Total requests: {0}", processingServer.requestCount);
            Console.WriteLine("Processed requests: {0}", processingServer.processedCount);
            Console.WriteLine("Rejected requests: {0}", processingServer.rejectedCount);

            Console.WriteLine("Idle probability. Actual: {0}. Theoretical: {1}.", actualValues.IdleProbability, theoryValues.IdleProbability);
            Console.WriteLine("Rejection probability. Actual: {0}. Theoretical: {1}.", actualValues.RejectionProbability, theoryValues.RejectionProbability);
            Console.WriteLine("Relative throughput. Actual: {0}. Theoretical: {1}.", actualValues.ThroughputRelative, theoryValues.ThroughputRelative);
            Console.WriteLine("Absolute throughput. Actual: {0}. Theoretical: {1}.", actualValues.ThroughputAbsolute, theoryValues.ThroughputAbsolute);
            Console.WriteLine("Average busy channels. Actual: {0}. Theoretical: {1}.", actualValues.BusyChannelsAverage, theoryValues.BusyChannelsAverage);

            if (!File.Exists(outputFile))
            {
                throw new FileNotFoundException($"File not found at {outputFile}.");
            }

            string resultLine = $"\n{arrivalRate:F5}; {serviceRate:F5}; " +
                $"{actualValues.IdleProbability:F5} - {theoryValues.IdleProbability:F5}; " +
                $"{actualValues.RejectionProbability:F5} - {theoryValues.RejectionProbability:F5}; " +
                $"{actualValues.ThroughputRelative:F5} - {theoryValues.ThroughputRelative:F5}; " +
                $"{actualValues.ThroughputAbsolute:F5} - {theoryValues.ThroughputAbsolute:F5}; " +
                $"{actualValues.BusyChannelsAverage:F5} - {theoryValues.BusyChannelsAverage:F5}";
            File.AppendAllText(outputFile, resultLine);
        }

        static int ComputeFactorial(int number)
        {
            return Enumerable.Range(1, number)
                .Aggregate(1, (result, value) => result * value);
        }

        static void FilterGraphData(string sourceFile, string filteredFile, string targetServiceRate)
        {
            var relevantLines = File.ReadLines(sourceFile)
                            .Where(line => line.Split(';').Skip(1).FirstOrDefault()?.Trim() == targetServiceRate)
                            .ToList();

            File.WriteAllLines(filteredFile, relevantLines);
        }

        static void GenerateVisualizations(string dataSource, string outputDirectory)
        {
            var dataLines = File.ReadAllLines(dataSource);

            string[] chartTitles = {
        "Idle probability - P0",
        "Rejection probability - Pn",
        "Relative throughput - Q",
        "Absolute throughput - A",
        "Average busy channels - k"
    };

            for (int chartIndex = 0; chartIndex < 5; chartIndex++)
            {
                int chartNumber = chartIndex + 1;

                double[] theoreticalValues = new double[dataLines.Length];
                double[] experimentalValues = new double[dataLines.Length];
                double[] arrivalRates = new double[dataLines.Length];

                for (int i = 0; i < dataLines.Length; i++)
                {
                    string[] columns = dataLines[i].Split(';')
                        .Select(p => p.Trim())
                        .ToArray();

                    arrivalRates[i] = double.Parse(columns[0]);

                    var valuePairs = columns[chartIndex + 2].Split('-')
                        .Select(p => double.Parse(p))
                        .ToArray();
                    experimentalValues[i] = valuePairs[0];
                    theoreticalValues[i] = valuePairs[1];
                }

                var chart = new Plot();
                chart.Title($"{chartTitles[chartIndex]} vs λ", size: 17);
                chart.XLabel("λ");
                chart.YLabel(chartTitles[chartIndex]);

                Action<Scatter, string, Color> styleSeries = (series, label, color) =>
                {
                    series.LegendText = label;
                    series.Color = color;
                    series.MarkerSize = 6;
                    series.LineWidth = 3;
                };

                var experimentalSeries = chart.Add.Scatter(arrivalRates, experimentalValues);
                styleSeries(experimentalSeries, "Experimental data", Colors.Green);

                var theoreticalSeries = chart.Add.Scatter(arrivalRates, theoreticalValues);
                styleSeries(theoreticalSeries, "Theoretical data", Colors.HotPink);

                chart.ShowLegend();

                string chartPath = Path.Combine(outputDirectory, $"p-{chartNumber}.png");
                chart.SavePng(chartPath, 1400, 900);
            }
        }
        static void Main(string[] args)
        {
            string rootDirectory = AppContext.BaseDirectory;
            string projectFolder = Path.GetFullPath(Path.Combine(rootDirectory, @"..\..\.."));
            string resultsFolder = Path.Combine(projectFolder, "result/");
            string metricsFile = Path.Combine(projectFolder, "results.txt");
            string chartDataFile = Path.Combine(projectFolder, "data.txt");

            double[] arrivalRates = { 5, 10, 15, 20, 25, 30 };
            double[] serviceRates = { 1, 5 };
            foreach (double rate in arrivalRates)
            {
                foreach (double serviceRate in serviceRates)
                {
                    CalculateAndCompareMetrics(rate, serviceRate, 5, metricsFile);
                }
            }

            FilterGraphData(metricsFile, chartDataFile, "5,00000");
            GenerateVisualizations(chartDataFile, resultsFolder);
        }
        struct ThreadPoolSlot
        {
            public Thread WorkerThread;
            public bool IsActive;
        }
        class Server
        {
            private double _time;

            private bool _isMonitoring = true;

            private int _totalChecks = 0;

            private int _idleChecks = 0;

            private int _totalOccupiedThreads = 0;

            private ThreadPoolSlot[] pool;
            private object threadLock = new object();
            public int requestCount = 0;
            public int processedCount = 0;
            public int rejectedCount = 0;
            public int TotalChecks => _totalChecks;
            public int IdleChecks => _idleChecks;
            public int TotalOccupiedThreads => _totalOccupiedThreads;
            public Server(double time, int threadPoolSize)
            {
                pool = new ThreadPoolSlot[threadPoolSize];
                _time = time;
            }
            public void proc(object sender, procEventArgs e)
            {
                lock (threadLock)
                {
                    Console.WriteLine("Заявка с номером: {0}", e.id);
                    requestCount++;
                    for (int i = 0; i < pool.Length; i++)
                    {
                        if (!pool[i].IsActive)
                        {
                            pool[i].IsActive = true;
                            pool[i].WorkerThread = new Thread(new ParameterizedThreadStart(Answer));
                            pool[i].WorkerThread.Start(e.id);
                            processedCount++;
                            return;
                        }
                    }
                    rejectedCount++;
                }
            }
            public void Answer(object arg)
            {
                int id = (int)arg;
                Console.WriteLine("Обработка заявки: {0}", id);
                Thread.Sleep((int)_time);

                for (int i = 0; i < pool.Length; i++)
                    if (pool[i].WorkerThread == Thread.CurrentThread)
                        pool[i].IsActive = false;
            }
            public void StartMonitoring()
            {
                Task.Run(() =>
                {
                    while (_isMonitoring)
                    {
                        _totalChecks++;
                        if (IsThreadPoolIdle())
                        {
                            _idleChecks++;
                        }

                        _totalOccupiedThreads += GetOccupiedThreadsCount();

                    }
                });
            }
            private bool IsThreadPoolIdle()
            {
                return !pool.Any(record => record.IsActive);
            }
            private int GetOccupiedThreadsCount()
            {
                return pool.Count(record => record.IsActive);
            }
            public void StopMonitoring()
            {
                _isMonitoring = false;
            }
        }
        class Client
        {
            private Server server;
            public Client(Server server)
            {
                this.server = server;
                this.request += server.proc;
            }
            public void send(int id)
            {
                procEventArgs args = new procEventArgs();
                args.id = id;
                OnProc(args);
            }
            protected virtual void OnProc(procEventArgs e)
            {
                EventHandler<procEventArgs> handler = request;
                if (handler != null)
                {
                    handler(this, e);
                }
            }
            public event EventHandler<procEventArgs> request;
        }
        public class procEventArgs : EventArgs
        {
            public int id { get; set; }
        }
    }
}