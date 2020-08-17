namespace SampleModule
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Runtime.Loader;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Linq;
    using Newtonsoft.Json;
    using System.Text.RegularExpressions;

    class Program
    {
        #region Classes
        public class Person
        {
            public string Name { get; set; }
            public string Height { get; set; }
            public string Mass { get; set; }
            public string Hair_color { get; set; }
            public string Skin_color { get; set; }
            public string Eye_color { get; set; }
            public string Birth_year { get; set; }
            public string Gender { get; set; }
            public string Homeworld { get; set; }
            public List<string> Films { get; set; }
            public List<string> Species { get; set; }
            public List<string> Starships { get; set; }
            public string Created { get; set; }
            public string Edited { get; set; }
            public string Url { get; set; }
        }

        public class Film
        {
            public string Title { get; set; }
            public int Episode_id { get; set; }
            public string Opening_crawl { get; set; }
            public string Director { get; set; }
            public string Producer { get; set; }
            public string Release_date { get; set; }
            public List<string> Characters { get; set; }
            public List<string> Planets { get; set; }
            public List<string> Starships { get; set; }
            public List<string> Vehicles { get; set; }
            public List<string> Species { get; set; }
            public string Created { get; set; }
            public string Edited { get; set; }
            public string Url { get; set; }
        }


        public class Starship
        {
            public string Name { get; set; }
            public string Model { get; set; }
            public string Manufacturer { get; set; }
            public string Length { get; set; }
            public string Max_atmosphering_speed { get; set; }
            public string Crew { get; set; }
            public string Passengers { get; set; }
            public string Cargo_capacity { get; set; }
            public string Consumables { get; set; }
            public string Hyperdrive_rating { get; set; }
            public string MGLT { get; set; }
            public string Starship_class { get; set; }
            public List<string> Pilots { get; set; }
            public List<string> Films { get; set; }
            public string Created { get; set; }
            public string Edited { get; set; }
            public string Url { get; set; }
        }

        public class MovieInfo
        {
            public string Movie { get; set; }
            public int Episode { get; set; }
            public string Released { get; set; }
        }

        public class StarshipInfo
        {
            public List<string> Pilots { get; set; }
            public string Passengers { get; set; }
        }
        #endregion

        static int counter;
        private static int s_telemetryInterval = 1; // Seconds

        static HttpClient _client = new HttpClient();
        static readonly string URL = "https://swapi.dev/api/";

        static bool test = true;

        static void Main(string[] args)
        {
            Init().Wait();

            // Wait until the app unloads or is cancelled
            var cts = new CancellationTokenSource();
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            WhenCancelled(cts.Token).Wait();
        }

        /// <summary>
        /// Handles cleanup operations when app is cancelled or unloads
        /// </summary>
        public static Task WhenCancelled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }

        /// <summary>
        /// Initializes the ModuleClient and sets up the callback to receive
        /// messages containing temperature information
        /// </summary>
        static async Task Init()
        {
            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };

            _client.BaseAddress = new Uri(URL);

            // Open a connection to the Edge runtime
            ModuleClient ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
            await ioTHubModuleClient.OpenAsync();
            Console.WriteLine("IoT Hub module client initialized 1.");

            // Register callback to be called when a message is received by the module
            //await ioTHubModuleClient.SetInputMessageHandlerAsync("input1", PipeMessage, ioTHubModuleClient);

            await ioTHubModuleClient.SetMethodHandlerAsync("CharacterMovies", CharacterMovies, ioTHubModuleClient);
            await ioTHubModuleClient.SetMethodHandlerAsync("StarshipsInfo", StarshipsInfo, ioTHubModuleClient);

        }

        private static async void SendFilmsToCloudMessagesAsync(List<Film> films, object userContext)
        {
            var moduleClient = userContext as ModuleClient;
            foreach (var film in films)
            {
                Console.WriteLine("Title: " + film.Title);
                var data = new
                {
                    Movie = film.Title
                    // Episode = film.Episode_id,
                    // Released = film.Release_date
                };
                var messageString = JsonConvert.SerializeObject(data);
                var message = new Message(Encoding.ASCII.GetBytes(messageString));

                await moduleClient.SendEventAsync("output1", message);
            }
        }

        private static Task<MethodResponse> StarshipsInfo(MethodRequest methodRequest, object userContext)
        {
            string starshipName = Encoding.UTF8.GetString(methodRequest.Data);

            StarshipInfo starshipInfo = null;

            string urlParameters = "starships/";
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            HttpResponseMessage response = _client.GetAsync(urlParameters).Result;

            if (response.IsSuccessStatusCode)
            {
                var jsonString = response.Content.ReadAsStringAsync();

                var dictionary = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonString.Result);
                var result = dictionary["results"];

                var starships = JsonConvert.DeserializeObject<List<Starship>>(result.ToString());

                var starship = starships.Where(p => p.Name.Equals(starshipName.Replace("\"", string.Empty), StringComparison.CurrentCultureIgnoreCase)).FirstOrDefault();

                Console.WriteLine("Starship: " + starship);

                var passengers = starship.Passengers;
                var pilots = starship.Pilots;
                var pilotNames = new List<string>();

                foreach (var pilot in pilots)
                {
                    urlParameters = pilot.Substring(URL.Length - 1, pilot.Length - URL.Length + 1);
                    response = _client.GetAsync(urlParameters).Result;
                    if (response.IsSuccessStatusCode)
                    {
                        var pilotName = JsonConvert.DeserializeObject<Dictionary<string, object>>(response.Content.ReadAsStringAsync().Result)["name"];
                        pilotNames.Add(pilotName.ToString());
                    }
                    else
                    {
                        Console.WriteLine("{0} ({1})", (int)response.StatusCode, response.ReasonPhrase);
                    }
                }

                starshipInfo = new StarshipInfo { Pilots = pilotNames, Passengers = passengers };
            }
            else
            {
                Console.WriteLine("{0} ({1})", (int)response.StatusCode, response.ReasonPhrase);
            }

            return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(starshipInfo)), 200));

        }
        private static Task<MethodResponse> CharacterMovies(MethodRequest methodRequest, object userContext)
        {
            try
            {
                string characterName = Encoding.UTF8.GetString(methodRequest.Data);
                var movies = new List<MovieInfo>();

                string urlParameters = "people/";
                _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                HttpResponseMessage response = _client.GetAsync(urlParameters).Result;

                if (response.IsSuccessStatusCode)
                {
                    var jsonString = response.Content.ReadAsStringAsync();

                    var dictionary = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonString.Result);
                    var result = dictionary["results"];

                    Console.WriteLine("Result" + result);

                    var persons = JsonConvert.DeserializeObject<List<Person>>(result.ToString());


                    var person = persons.Where(p => p.Name.Equals(characterName.Replace("\"", String.Empty), StringComparison.CurrentCultureIgnoreCase)).FirstOrDefault();

                    //person = persons[0];
                    Console.WriteLine("Person: " + person);

                    var movieList = person.Films;

                    List<Film> films = new List<Film>();

                    Console.WriteLine("Films: " + films);

                    foreach (var movie in movieList)
                    {
                        urlParameters = movie.Substring(URL.Length - 1, movie.Length - URL.Length + 1);
                        response = _client.GetAsync(urlParameters).Result;
                        if (response.IsSuccessStatusCode)
                        {
                            var film = JsonConvert.DeserializeObject<Film>(response.Content.ReadAsStringAsync().Result);
                            films.Add(film);
                            movies.Add(new MovieInfo { Movie = film.Title, Episode = film.Episode_id, Released = film.Release_date });
                        }
                        else
                        {
                            string error = $"{{({(int)response.StatusCode}, {response.ReasonPhrase}}}";
                            return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(error), 400));
                        }
                    }
                    // sort by release date
                    films.Sort((a, b) => ((Convert.ToDateTime(a.Release_date)).CompareTo(Convert.ToDateTime(b.Release_date))));

                    Console.WriteLine("Send films");
                    SendFilmsToCloudMessagesAsync(films, userContext);
                }
                else
                {
                    string error = $"{{({(int)response.StatusCode}, {response.ReasonPhrase}}}";
                    return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(error), 400));
                }
                return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(movies)), 200));
            }
            catch (Exception ex)
            {
                string error = $"{{({ex.Message}}}";
                return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(error), 500));
            }

        }

        /// <summary>
        /// This method is called whenever the module is sent a message from the EdgeHub. 
        /// It just pipe the messages without any change.
        /// It prints all the incoming messages.
        /// </summary>
        static async Task<MessageResponse> PipeMessage(Message message, object userContext)
        {
            int counterValue = Interlocked.Increment(ref counter);

            var moduleClient = userContext as ModuleClient;
            if (moduleClient == null)
            {
                throw new InvalidOperationException("UserContext doesn't contain " + "expected values");
            }

            byte[] messageBytes = message.GetBytes();
            string messageString = Encoding.UTF8.GetString(messageBytes);
            Console.WriteLine($"Received message: {counterValue}, Body: [{messageString}]");

            if (!string.IsNullOrEmpty(messageString))
            {
                using (var pipeMessage = new Message(messageBytes))
                {
                    foreach (var prop in message.Properties)
                    {
                        pipeMessage.Properties.Add(prop.Key, prop.Value);
                    }
                    await moduleClient.SendEventAsync("output1", pipeMessage);

                    Console.WriteLine("Received message sent");
                }
            }
            return MessageResponse.Completed;
        }
    }
}
