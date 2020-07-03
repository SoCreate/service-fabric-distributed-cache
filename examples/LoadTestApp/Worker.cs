using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LoadTestApp
{
    class Worker : IHostedService
    {
        private readonly IDistributedCache _distributedCache;
        private Stopwatch _stopWatch = new Stopwatch();
        private List<string> _timeMeasures = new List<string>();

        public Worker(IDistributedCache distributedCache)
        {
            _distributedCache = distributedCache;
        }
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            byte[] bytes = Encoding.ASCII.GetBytes("Lorem Ipsum is simply dummy text of the printing and typesetting industry. Lorem Ipsum has been the industry's standard dummy text ever since the 1500s, when an unknown printer took a galley of type and scrambled it to make a type specimen book. It has survived not only five centuries, but also the leap into electronic typesetting, remaining essentially unchanged. It was popularised in the 1960s with the release of Letraset sheets containing Lorem Ipsum passages, and more recently with desktop publishing software like Aldus PageMaker including versions of Lorem Ipsum");
            var options = new DistributedCacheEntryOptions();
            options.SlidingExpiration = TimeSpan.FromSeconds(20);
            
            var tasks = new List<Task<string>>();

            for (var i = 0; i < 5000; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _timeMeasures.Add(MeasureTime(() => {
                    _distributedCache.Set($"SetKey2-{i}", bytes, options);
                }, "SetKey"));
            }

            for (var i = 0; i < 5000; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _timeMeasures.Add(await MeasureTimeAsync(async () => {
                    await _distributedCache.SetAsync($"SetAsyncAwaitedKey2-{i}", bytes, options, cancellationToken);
                }, "SetAsyncAwaitedKey"));
            }

            for (var i = 0; i < 5000; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                tasks.Add(MeasureTimeAsync(async () => { 
                    await _distributedCache.SetAsync($"SetAsyncAwaitedWhenAllKey2-{i}", bytes, options, cancellationToken);
                }, "SetAsyncAwaitedWhenAllKey"));
            }
            _timeMeasures.AddRange(await Task.WhenAll(tasks));

            foreach (var timeMeasure in _timeMeasures)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Console.WriteLine(timeMeasure);
            }

            Console.WriteLine("Done!");
            Console.ReadKey();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private async Task<string> MeasureTimeAsync(Func<Task> action, string label) 
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            await action();
            _stopWatch.Stop();
            return $"{label} {stopWatch.ElapsedMilliseconds}";
        }

        private string MeasureTime(Action action, string label)
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            action();
            _stopWatch.Stop();
            return $"{label} {stopWatch.ElapsedMilliseconds}";
        }
    }
}
