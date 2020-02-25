namespace worker
{
    using Newtonsoft.Json;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Queue;
    using System;
    using System.Data.SqlClient;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    class Program
    {
        static string StorageAccountName = Environment.GetEnvironmentVariable("AZURE_STORAGE_ACCOUNT").Trim();
        static string StorageAccountKey = Environment.GetEnvironmentVariable("AZURE_STORAGE_ACCESS_KEY").Trim();
	static string SQLHostName = Environment.GetEnvironmentVariable("SQL_HOSTNAME").Trim();
        static string SQLUserName = Environment.GetEnvironmentVariable("SQL_USERNAME").Trim();
        static string SQLPassword = Environment.GetEnvironmentVariable("SQL_PASSWORD").Trim();

        static void Main(string[] args)
        {
            var cts = new CancellationTokenSource();

            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            Console.WriteLine($"StorageAccountName: {StorageAccountName}");
            if (String.IsNullOrEmpty(StorageAccountName))
            {
                throw new ArgumentNullException(nameof(StorageAccountName));
            }

            Console.WriteLine($"StorageAccountKey: {StorageAccountKey}");
            if (String.IsNullOrEmpty(StorageAccountKey))
            {
                throw new ArgumentNullException(nameof(StorageAccountKey));
            }

            var storageAccount = CloudStorageAccount.Parse($"DefaultEndpointsProtocol=https;AccountName={StorageAccountName};AccountKey={StorageAccountKey};EndpointSuffix=core.windows.net");

            var queueClient = storageAccount.CreateCloudQueueClient();
            var queue = queueClient.GetQueueReference("votes");
            queue.CreateIfNotExistsAsync().Wait(cts.Token);

            var sqlConnectionbuilder = new SqlConnectionStringBuilder()
            {
                DataSource = SQLHostName,
                UserID = SQLUserName,
                Password = SQLPassword,
                InitialCatalog = "VOTEDB"
            };

	    Console.WriteLine($"SQL ConnectionString: {sqlConnectionbuilder.ConnectionString}");
            Console.WriteLine($"Starting worker...");

            using (var sqlConnection = new SqlConnection(sqlConnectionbuilder.ConnectionString))
            {
                sqlConnection.Open();

                try
                {
                    while (!cts.IsCancellationRequested)
                    {
                        CheckQueueAsync(queue, sqlConnection).Wait(cts.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    // swallow
                }
                finally
                {
                    cts.Dispose();
                }
            }
        }

        private static async Task CheckQueueAsync(CloudQueue queue, SqlConnection sqlConnection)
        {
            CloudQueueMessage retrievedMessage = await queue.GetMessageAsync();
            if (retrievedMessage == null)
            {
                return;
            }

            dynamic record = JsonConvert.DeserializeObject(retrievedMessage.AsString);
            Console.WriteLine($"Processing {record.voter_id} with {record.vote}");
            var sb = new StringBuilder();
            sb.AppendLine($"UPDATE votes SET vote='{record.vote}' where id='{record.voter_id}'");
            sb.AppendLine("IF @@ROWCOUNT = 0");
            sb.AppendLine($"INSERT INTO votes VALUES('{record.voter_id}', '{record.vote}')");
	    var sql = sb.ToString();
            var command = new SqlCommand(sql, sqlConnection);
            command.ExecuteNonQuery();

	    // Update counts
	    sb.Clear();
	    sb.AppendLine("UPDATE voteCount");
	    sb.AppendLine("SET count=(SELECT COUNT(*) FROM votes WHERE votes.vote=voteCount.vote)");
            sql = sb.ToString();
            command = new SqlCommand(sql, sqlConnection);
            command.ExecuteNonQuery();

	    // Message processing completed
            await queue.DeleteMessageAsync(retrievedMessage);
        }
    }
}
