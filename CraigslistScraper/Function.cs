using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;

using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;

using Newtonsoft.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace CraigslistScraper
{
    public class Functions
    {
        public const string CITY_NAME = "city";
        public IDynamoDBContext DBContext { get; set; }

        private string GigURL = "https://{0}.craigslist.org/search/cpg?is_paid=yes&postedToday=1";

        private string JobURL = "https://{0}.craigslist.org/search/sof?postedToday=1&is_telecommuting=1";

        /// <summary>
        /// Default constructor that Lambda will invoke.
        /// </summary>
        public Functions()
        {
            string tableName = "CraigslistJobs";
            AWSConfigsDynamoDB.Context.TypeMappings[typeof(Listing)] = new Amazon.Util.TypeMapping(typeof(Listing), tableName);
            DynamoDBContextConfig config = new DynamoDBContextConfig { Conversion = DynamoDBEntryConversion.V2 };
            this.DBContext = new DynamoDBContext(new AmazonDynamoDBClient(), config);
        }


        /// <summary>
        /// A Lambda function to respond to HTTP Get methods from API Gateway
        /// </summary>
        /// <param name="request"></param>
        /// <returns>The list of blogs</returns>
        public async Task<APIGatewayProxyResponse> Get(APIGatewayProxyRequest request, ILambdaContext context)
        {
            context.Logger.LogLine("Get Request\n");

            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = "Hello AWS Serverless",
                Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
            };

            return response;
        }

        public async Task<APIGatewayProxyResponse> AddListing(APIGatewayProxyRequest request, ILambdaContext context)
        {
            string cityName = null;
            if (request.PathParameters != null && request.PathParameters.ContainsKey(CITY_NAME))
            {
                cityName = request.PathParameters[CITY_NAME];
            }
            else
            {
                cityName = "losangeles";
            }

            string content = GetData(cityName, this.GigURL);
            List<Listing> listings = ParseHTML(content, cityName);

            string jobContent = GetData(cityName, this.JobURL);
            listings.AddRange(ParseHTML(jobContent, cityName));

            foreach (Listing listing in listings)
            {
                await DBContext.SaveAsync<Listing>(listing);
            }

            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = string.Format("{0} listings saved in {1}", listings.Count, "LosAngeles"),
                Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
            };
            return response;
        }

        private List<Listing> ParseHTML(string content, string cityName)
        {
            string regexAnchor = @"<a href=""([\w\:\.\-\/]+\d+\.html)""[^>]*?>(.+)(?=</a>)";
            string regexDate = @"<time class=""result-date"" datetime=""(\d{4}-\d{2}-\d{2} \d{2}:\d{2})""[^>]*?>";
            List<Listing> listings = new List<Listing>();

            if (content.Contains("<li class=\"result - row\""))
            {
                MatchCollection dateMatches = Regex.Matches(content, regexDate);
                MatchCollection anchorMatches = Regex.Matches(content, regexAnchor);

                if (dateMatches != null && dateMatches.Count > 0)
                {
                    for (int i = 0; i < dateMatches.Count; i++)
                    {
                        Match match = dateMatches[i];
                        for (int groupIndex = 0; groupIndex < match.Groups.Count; groupIndex++)
                        {
                            Group group = match.Groups[groupIndex];
                            if (!group.Value.StartsWith("<time"))
                            {
                                Listing listing = new Listing();
                                listing.ListingDate = group.Value;
                                listing.Title = anchorMatches[i].Groups[groupIndex + 1].Value;
                                listing.Link = anchorMatches[i].Groups[groupIndex].Value;
                                listing.City = cityName;
                                listings.Add(listing);
                            }
                        }


                    }
                }
            }
            return listings;
        }
        private string GetData(string city, string URL)
        {
            StreamReader reader = null;
            string content = null;
            try
            {
                HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(string.Format(URL, city));
                httpWebRequest.ContentType = "";
                httpWebRequest.Method = "GET";
                WebResponse webResponse = httpWebRequest.GetResponse();

                reader = new StreamReader(webResponse.GetResponseStream());
                content = reader.ReadToEnd();

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
                if (ex.InnerException != null)
                {
                    Console.WriteLine("Inner Exception:");
                    Console.WriteLine(ex.InnerException.Message);
                    Console.WriteLine(ex.InnerException.StackTrace);
                }
            }
            finally
            {
                if (reader != null)
                {
                    reader.Close();
                }
                reader = null;
            }
           
            return content;
        }
    }
}
