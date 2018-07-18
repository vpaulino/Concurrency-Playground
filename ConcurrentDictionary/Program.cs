using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace ConcurrentDictionary
{

    public class DisposableObject : IDisposable
    {

        public DisposableObject(string id)
        {
            this.Id = id;
        }
        public string Id
        {
            get;
            set;
        }


        public void Dispose()
        {
            
        }
    }

    public class ManageDisposableObjectsInConcurrentDictionary : IRepository
    {

        ConcurrentDictionary<string, DisposableObject> repository = new ConcurrentDictionary<string, DisposableObject>();
        public ConcurrentBag<TimeSpan> ExecutionTimes { get; set; } = new ConcurrentBag<TimeSpan>();
        public string Type { get; set; } = "Concurrent";

        public void Add(DisposableObject instance)
        {
            Stopwatch countTime = new Stopwatch();
            countTime.Start(); 
            repository.TryAdd(instance.Id, instance);
            countTime.Stop();
            ExecutionTimes.Add(countTime.Elapsed);
           // Console.WriteLine($"Add lock took: {countTime.Elapsed}");
        }

        public void Remove(string id)
        {


            Stopwatch countTime = new Stopwatch();
            countTime.Start();
                
            repository.TryRemove(id, out var removed);

            countTime.Stop();
            ExecutionTimes.Add(countTime.Elapsed);
            // Console.WriteLine($"Remove lock took: {countTime.Elapsed}");

        } 


        public void Clear()
        {

            Stopwatch countTime = new Stopwatch();
            countTime.Start();
             
            foreach (var item in repository)
            {
                item.Value.Dispose();
            }
            countTime.Stop();
            ExecutionTimes.Add(countTime.Elapsed);
           
            repository.Clear();
        }
    }

     
    public class ManageDisposableObjectsInLockedDictionary : IRepository
    {

        Dictionary<string, DisposableObject> repository = new Dictionary<string, DisposableObject>();
        public ConcurrentBag<TimeSpan> ExecutionTimes { get; set; } = new  ConcurrentBag<TimeSpan>();
        public string Type { get; set; } = "Locked";

        object lockObj = new object();

        public void Add(DisposableObject instance)
        {
            Stopwatch countTime = new Stopwatch();
            countTime.Start();
            lock (lockObj)
            {   
              //  Console.WriteLine($"Add lock took: {countTime.Elapsed}");
                repository.Add(instance.Id, instance);
                countTime.Stop();
            }

            ExecutionTimes.Add(countTime.Elapsed);

        }

        public void Remove(string id)
        {


            Stopwatch countTime = new Stopwatch();
            countTime.Start();
            lock (lockObj)
            {
              
              //  Console.WriteLine($"Remove lock took: {countTime.Elapsed}");
                repository.Remove(id);
                countTime.Stop();
            }
            ExecutionTimes.Add(countTime.Elapsed);

        }
         

        public void Clear()
        {

            Stopwatch countTime = new Stopwatch();
            countTime.Start();
            lock (lockObj)
            {  

                foreach (var item in repository.Values)
                {
                    item.Dispose();
                }
                repository.Clear();
                countTime.Stop();
                ExecutionTimes.Add(countTime.Elapsed);

            }
        }
    }

    public class Result
    {
        public string OperationName { get; set; }

        public TimeSpan Overall { get; set; }

        public TimeSpan MaxPerAction { get; set; }

        public string Type { get; set; }
        public long Executions { get; internal set; }
    }
    class Program
    {
        static void Main(string[] args)
        {


            List<Result> results = new List<Result>();
            Console.WriteLine("Starting... press to continue");
            Console.ReadKey();
            Console.WriteLine("Executing .. ");


            Console.WriteLine("Number Of records per operations : ");
            var line = Console.ReadLine();
            long numberOfRcords = long.Parse(line);
            for (long executions = 0; executions <= numberOfRcords; executions = executions + (numberOfRcords/10))
            {

                //Console.WriteLine("Starting... press to continue");
                //Console.ReadKey();
                //Console.WriteLine("Executing .. ");
                ManageDisposableObjectsInLockedDictionary repo = new ManageDisposableObjectsInLockedDictionary();
                Run(repo, executions, results);

                //Console.WriteLine("Press to continue... ");
                //Console.ReadKey();
                //Console.WriteLine("Executing .. ");
                ManageDisposableObjectsInConcurrentDictionary repo2 = new ManageDisposableObjectsInConcurrentDictionary();
                Run(repo2, executions, results);
                //Console.WriteLine("Ended");
                //Console.ReadKey();

                 
            }

            Show(results);

            Console.ReadLine();



        }

     

        private static void Run(IRepository repository, long executions, List<Result> results)
        {   

            (List<DisposableObject> toRemove, List<DisposableObject> toAdd) SetupDataSources()
            {
                List<DisposableObject> elementsToAdd = new List<DisposableObject>();

                for (long idx = 0; idx <= executions; idx++)
                {
                    elementsToAdd.Add(new DisposableObject(Guid.NewGuid().ToString()));
                }

                List<DisposableObject> elementsToRemove = new List<DisposableObject>();

                for (long idx = 0; idx <= executions; idx++)
                {
                    elementsToRemove.Add(new DisposableObject(Guid.NewGuid().ToString()));
                }
              var result =  Parallel.ForEach(elementsToRemove, (instance) => repository.Add(instance));

                while (!result.IsCompleted) ;

                return (elementsToRemove, elementsToAdd);
            }


            var dataSources = SetupDataSources();


           Stopwatch overallExecTime = new Stopwatch();
            overallExecTime.Start();

            Parallel.Invoke(new ParallelOptions() { MaxDegreeOfParallelism = 100 },
                () => 
                {
                    var result = Parallel.ForEach(dataSources.toAdd, (instance) => repository.Add(instance));
                    while (!result.IsCompleted) ;
                },
                () => 
                {
                    var result = Parallel.ForEach(dataSources.toRemove, (instance) => repository.Remove(instance.Id));
                    while (!result.IsCompleted) ;
                },
                () => 
                {
                    repository.Clear();
                }
                );
           
             
            overallExecTime.Stop();
            TimeSpan maxDuration = repository.ExecutionTimes.Max();
            results.Add(new Result() { OperationName = " Add ", Executions = executions, MaxPerAction = maxDuration, Overall = overallExecTime.Elapsed, Type = repository.Type });
             
            
        }

        private static void Show(List<Result> results)
        {
            Console.Clear();
            Console.WriteLine($" Executions | Type | Op |  Overall Exec Time | MaxPerAction Exec Time ");

            var groups = results.OrderByDescending((result) => result.MaxPerAction).GroupBy(result => result.Executions);

            foreach (var group in groups)
            {
                var groupResults = group.ToList();
                var orderedByOVerall = groupResults.OrderByDescending(result => result.MaxPerAction);
                foreach (var item in orderedByOVerall)
                {
                    Console.WriteLine($" {item.Executions} | {item.Type} |{item.OperationName}| {item.Overall} | {item.MaxPerAction}");
                }
                Console.WriteLine($"-----------------------------------------------------------------------");
            }

        }



    }
}
