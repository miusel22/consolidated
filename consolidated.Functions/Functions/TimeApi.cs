using consolidated.Common.Models;
using consolidated.Common.Response;
using consolidated.Functions.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace consolidated.Functions.Functions
{
    public static class TimeApi
    {
        [FunctionName("CreateTime")]
        public static async Task<IActionResult> CreateTime(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "time")] HttpRequest req,
            [Table("time", Connection = "AzureWebJobsStorage")] CloudTable timeTable,
            ILogger log)
        {
            log.LogInformation("Recieved a new time of employed.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            Time time = JsonConvert.DeserializeObject<Time>(requestBody);

            if (string.IsNullOrEmpty(time?.employeId.ToString()))
            {
                return new BadRequestObjectResult(new Response
                {
                    IsSuccess = false,
                    Message = "The request must have a employed ID."
                });
            }

            TimeEntity timeEntity = new TimeEntity
            {
                employeId = time.employeId,
                date = time.date,
                type = time.type,
                ETag = "*",
                isConsolidated = false,
                PartitionKey = "TIME",
                RowKey = Guid.NewGuid().ToString(),

            };

            TableOperation addOperation = TableOperation.Insert(timeEntity);
            await timeTable.ExecuteAsync(addOperation);

            string message = "New time of employe stored in table";
            log.LogInformation(message);

            return new OkObjectResult(new Response
            {
                IsSuccess = true,
                Message = message,
                Result = timeEntity
            });
        }

        [FunctionName(nameof(UpdateTime))]
        public static async Task<IActionResult> UpdateTime(
             [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "time/{id}")] HttpRequest req,
             [Table("time", Connection = "AzureWebJobsStorage")] CloudTable timeTable,
             string id,
             ILogger log)
        {
            log.LogInformation($"Update for time: {id}, received.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            Time time = JsonConvert.DeserializeObject<Time>(requestBody);

            // Validate time id
            TableOperation findOperation = TableOperation.Retrieve<TimeEntity>("TIME", id);
            TableResult findResult = await timeTable.ExecuteAsync(findOperation);
            if (findResult.Result == null)
            {
                return new BadRequestObjectResult(new Response
                {
                    IsSuccess = false,
                    Message = "Time not found."
                });
            }

            // Update Time
            TimeEntity timeEntity = (TimeEntity)findResult.Result;
            timeEntity.isConsolidated = time.isConsolidated;
            timeEntity.date = time.date;
            timeEntity.type = time.type;
            if (!string.IsNullOrEmpty(time.employeId.ToString()))
            {
                timeEntity.employeId = time.employeId;
            }

            TableOperation addOperation = TableOperation.Replace(timeEntity);
            await timeTable.ExecuteAsync(addOperation);

            string message = $"Time: {id}, updated in table.";
            log.LogInformation(message);

            return new OkObjectResult(new Response
            {
                IsSuccess = true,
                Message = message,
                Result = timeEntity
            });
        }
        [FunctionName(nameof(GetAllTimes))]
        public static async Task<IActionResult> GetAllTimes(
          [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "times")] HttpRequest req,
          [Table("time", Connection = "AzureWebJobsStorage")] CloudTable timeTable,
          ILogger log)
        {
            log.LogInformation("Get all times received.");

            TableQuery<TimeEntity> query = new TableQuery<TimeEntity>();
            TableQuerySegment<TimeEntity> times = await timeTable.ExecuteQuerySegmentedAsync(query, null);

            string message = "Retrieved all times.";
            log.LogInformation(message);

            return new OkObjectResult(new Response
            {
                IsSuccess = true,
                Message = message,
                Result = times
            });
        }

        [FunctionName(nameof(GetTimeById))]
        public static IActionResult GetTimeById(
                   [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "time/{id}")] HttpRequest req,
                   [Table("time", "TIME", "{id}", Connection = "AzureWebJobsStorage")] TimeEntity timeEntity,
                   string id,
                   ILogger log)
        {
            log.LogInformation($"Get time by id: {id}, received.");

            if (timeEntity == null)
            {
                return new BadRequestObjectResult(new Response
                {
                    IsSuccess = false,
                    Message = "Time not found."
                });
            }

            string message = $"Time: {timeEntity.RowKey}, retrieved.";
            log.LogInformation(message);

            return new OkObjectResult(new Response
            {
                IsSuccess = true,
                Message = message,
                Result = timeEntity
            });
        }

        [FunctionName(nameof(DeleteTime))]
        public static async Task<IActionResult> DeleteTime(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "time/{id}")] HttpRequest req,
            [Table("time", "TIME", "{id}", Connection = "AzureWebJobsStorage")] TimeEntity timeEntity,
            [Table("time", Connection = "AzureWebJobsStorage")] CloudTable timeTable,
            string id,
            ILogger log)


        {
            log.LogInformation($"Delete time: {id}, received.");

            if (timeEntity == null)
            {
                return new BadRequestObjectResult(new Response
                {
                    IsSuccess = false,
                    Message = "Time not found."
                });
            }

            await timeTable.ExecuteAsync(TableOperation.Delete(timeEntity));
            string message = $"Time: {timeEntity.RowKey}, deleted.";
            log.LogInformation(message);

            return new OkObjectResult(new Response
            {
                IsSuccess = true,
                Message = message,
                Result = timeEntity
            });
        }


        [FunctionName("Consolidated")]
        public static async Task<IActionResult> Consolidated(
          [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "consolidated")] HttpRequest req,
          [Table("time", Connection = "AzureWebJobsStorage")] CloudTable timeTable,
           [Table("consolidated", Connection = "AzureWebJobsStorage")] CloudTable consolidatedTable,
          ILogger log)
        {
            try
            {
                log.LogInformation($"Received  a new consolidation process, start at: {DateTime.Now}");

                string filterTime = TableQuery.GenerateFilterConditionForBool("isConsolidated", QueryComparisons.Equal, false);

                TableQuery<TimeEntity> queryTime = new TableQuery<TimeEntity>().Where(filterTime);
                TableQuerySegment<TimeEntity> unconsolidatedTimes = await timeTable.ExecuteQuerySegmentedAsync(queryTime, null);

                List<TimeEntity> ordereUnconsolidatedTimes = unconsolidatedTimes.OrderBy(x => x.date).ThenBy(x =>
                 x.employeId).ToList();

                string message = string.Empty;
                List<consolidatedEntity> listConst = new List<consolidatedEntity>();
                if (ordereUnconsolidatedTimes?.Count > 1)
                {

                    for (int i = 0; i < ordereUnconsolidatedTimes.Count; i++)

                    {
                        if(i+1 == ordereUnconsolidatedTimes.Count)
                        {
                            continue;
                        }
                        consolidatedEntity consolidatedEntity = null;
                        if (ordereUnconsolidatedTimes[i].employeId == ordereUnconsolidatedTimes[i + 1].employeId)
                        {

                            TimeSpan workTime = ordereUnconsolidatedTimes[i + 1].date - ordereUnconsolidatedTimes[i].date;
                            DateTime date = new DateTime(ordereUnconsolidatedTimes[i].date.Year, ordereUnconsolidatedTimes[i].date.Month,
                            ordereUnconsolidatedTimes[i].date.Day);

                            TableOperation findEmploye = TableOperation.Retrieve<TimeEntity>("TIME", ordereUnconsolidatedTimes[i].employeId.ToString());
                            TableResult findResult = await timeTable.ExecuteAsync(findEmploye);


                            if (findResult.Result == null)
                            {
                                consolidatedEntity = new consolidatedEntity
                                {
                                    employeId = ordereUnconsolidatedTimes[i].employeId,
                                    date = date,
                                    MinutesWork = (int)workTime.TotalMinutes,
                                    ETag = "*",
                                    PartitionKey = "TIME",
                                    RowKey = Guid.NewGuid().ToString(),
                                };
                                TableOperation addConsolidate = TableOperation.Insert(consolidatedEntity);
                                //await timeTable.ExecuteAsync(addConsolidate);
                                await consolidatedTable.ExecuteAsync(addConsolidate);
                                string messageC = "New time of employe stored in table";
                                log.LogInformation(messageC);


                            }
                            else
                            {
                                // Update Employe
                                consolidatedEntity = (consolidatedEntity)findResult.Result;
                                consolidatedEntity.employeId = ordereUnconsolidatedTimes[i].employeId;
                                consolidatedEntity.date = date;
                                consolidatedEntity.MinutesWork += (int)workTime.TotalMinutes;


                                TableOperation addOperation = TableOperation.Replace(consolidatedEntity);
                                await consolidatedTable.ExecuteAsync(addOperation);

                                string messageT = $"Time: {ordereUnconsolidatedTimes[i].employeId}, updated in table.";
                                log.LogInformation(messageT);


                            }
                            listConst.Add(consolidatedEntity);



                            ordereUnconsolidatedTimes[i].isConsolidated = true;
                            TableOperation operation = TableOperation.Replace(ordereUnconsolidatedTimes[i]);
                            ordereUnconsolidatedTimes[i + 1].isConsolidated = true;
                            TableOperation operation2 = TableOperation.Replace(ordereUnconsolidatedTimes[i + 1]);
                            await timeTable.ExecuteAsync(operation2);
                            await timeTable.ExecuteAsync(operation);

                        }
                        //i = i + 1;

                    }




                }
                return new OkObjectResult(new Response
                {
                    IsSuccess = true,
                    Message = message,
                    Result = listConst
                });
            }
            catch (Exception ex)
            {

                log.LogInformation(ex.Message); throw ;
            }



        }
        [FunctionName(nameof(ConsolidateForDate))]
        public static async Task <IActionResult>ConsolidateForDate(
             [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "consolidate/{date:DateTime?}")] HttpRequest req,
             [Table("consolidate", Connection = "AzureWebJobsStorage")] CloudTable consolidatedTable,
             DateTime date,
             ILogger log)
        {
            log.LogInformation($"Get consolidate for date: {date}, received.");
            var query  = TableQuery.GenerateFilterConditionForDate("date", QueryComparisons.Equal,date);
            TableQuery<consolidatedEntity> queryTime = new TableQuery<consolidatedEntity>().Where(query);
            IEnumerable<consolidatedEntity> consolidateEntity = await consolidatedTable.ExecuteQuerySegmentedAsync(queryTime, null);

            if (consolidateEntity == null)
            {
                return new BadRequestObjectResult(new Response
                {
                    IsSuccess = false,
                    Message = "consolidate not found."
                });
            }

            string message = "success";
            log.LogInformation(message);

            return new OkObjectResult(new Response
            {
                IsSuccess = true,
                Message = message,
                Result = consolidateEntity
            });
        }

    }
}

