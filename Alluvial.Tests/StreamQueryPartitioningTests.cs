using System;
using System.Collections.Generic;
using FluentAssertions;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using Alluvial.Distributors;
using NUnit.Framework;

namespace Alluvial.Tests
{
    [TestFixture]
    public class StreamQueryPartitioningTests
    {
        private int[] ints;
        private IStreamQueryPartitioner<int, int, int> partitioner;
        private CompositeDisposable disposables;

        [SetUp]
        public void SetUp()
        {
            ints = Enumerable.Range(1, 1000).ToArray();
            disposables = new CompositeDisposable();
            partitioner = Stream
                .Partition<int, int, int>(async (q, p) => ints
                                              .Where(i => i > p.LowerBoundExclusive &&
                                                          i <= p.UpperBoundInclusive)
                                              .Skip(q.Cursor.Position)
                                              .Take(q.BatchCount.Value),
                                          advanceCursor: (query, batch) => { query.Cursor.AdvanceTo(batch.Last()); });
        }

        [Test]
        public async Task A_stream_can_be_partitioned_through_query_parameterization()
        {
            var partition = StreamQuery.Partition(0, 100);
            var stream = await partitioner.GetStream(partition);

            var aggregator = Aggregator.CreateFor<int, int>((p, i) => p.Value += i.Sum());

            var projection = await stream.Aggregate(aggregator, new Projection<int, int>());

            projection.Value
                      .Should()
                      .Be(Enumerable.Range(1, 100).Sum());
        }

        [Test]
        public async Task When_a_partition_is_queried_then_the_cursor_is_updated()
        {
            var partitions = new[]
            {
                StreamQuery.Partition(0, 500),
                StreamQuery.Partition(500, 1000)
            };

            var store = new InMemoryProjectionStore<Projection<int, int>>();

            await Task.WhenAll(partitions.Select(async partition =>
            {
                var stream = await partitioner.GetStream(partition);

                Console.WriteLine(stream);

                var aggregator = Aggregator.CreateFor<int, int>((p, i) => p.Value += i.Sum());

                var catchup = StreamCatchup.Create(stream);
                catchup.Subscribe(aggregator, store);
                await catchup.RunSingleBatch();
            }));

            store.Should()
                 .ContainSingle(p => p.CursorPosition == 500)
                 .And
                 .ContainSingle(p => p.CursorPosition == 1000);
        }

        [Ignore("Test not finished")]
        [Test]
        public async Task Competing_catchups_can_lease_a_partition_using_a_distributor()
        {
            var partitions = Enumerable.Range(0, 9)
                                       .Select(i => StreamQuery.Partition(i*100, (i + 1)*100))
                                       .ToArray();

            var store = new InMemoryProjectionStore<Projection<HashSet<int>, int>>();

            var aggregator = Aggregator.CreateFor<HashSet<int>, int>((p, xs) =>
            {
                if (p.Value == null)
                {
                    p.Value = new HashSet<int>();
                }

                foreach (var x in xs)
                {
                    p.Value.Add(x);
                }
            }).Trace();

            // set up 10 competing catchups
            for (var i = 0; i < 10; i++)
            {
                var distributor = new InMemoryStreamQueryDistributor(
                    partitions.Select(p => new LeasableResource(p.ToString(), TimeSpan.FromSeconds(10))).ToArray(), "")
                    .Trace();

                distributor.OnReceive(async lease =>
                {
                    var partition = partitions.Single(p => p.ToString() == lease.LeasableResource.Name);
                    var catchup = StreamCatchup.Create(await partitioner.GetStream(partition));
                    catchup.Subscribe(aggregator, store.Trace());
                    await catchup.RunSingleBatch();
                });

                distributor.Start();

                disposables.Add(distributor);
            }

            partitions.ToList()
                      .ForEach(partition =>
                                   store.Should()
                                        .ContainSingle(p =>
                                                           p.Value.Count() == 100 &&
                                                           p.Value.OrderBy(i => i).Last() == partition.UpperBoundInclusive));
        }
    }
}