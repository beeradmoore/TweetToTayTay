using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Twitterizer;

namespace TweetToTayTay
{
	// Classes all defined in the Program.cs because I was in a rush and they are all small.
	public class MessageObject
	{
		public string ID { get; set; }
		public string TwitterID { get; set; }
		public string Message { get; set; }
		public DateTime Sent { get; set; }
	}


	public class TwitAuthenticateResponse
	{
		public string token_type { get; set; }
		public string access_token { get; set; }
	}

	public class TTTTFriendsListResponse
	{
		[JsonProperty(PropertyName = "users")]
		public List<TTTTUsers> Users { get; set; }

	}

	public class TTTTUsers
	{
		[JsonProperty(PropertyName = "id")]
		public long ID { get; set; }

		[JsonProperty(PropertyName = "id_str")]
		public string IDString { get; set; }

		[JsonProperty(PropertyName = "name")]
		public string Name { get; set; }

		[JsonProperty(PropertyName = "screen_name")]
		public string ScreenName { get; set; }

		[JsonProperty(PropertyName = "location")]
		public string Location { get; set; }
	}


	class Program
	{
		private static List<MessageObject> _sentMessages = new List<MessageObject>();
		private static Random _rand = new Random();
		private static TTTTFriendsListResponse _friendsList = null;
		private static List<string> _friends = new List<string>();

		static void Main(string[] args)
		{
			// Load existing sent tweets. Was kept incase account was banned for spam I'd have a nice backup.
			try
			{
				if (File.Exists("sent.json"))
				{
					string fileData = File.ReadAllText("sent.json");
					List<MessageObject> oldMessages = JsonConvert.DeserializeObject<List<MessageObject>>(fileData);
					_sentMessages.AddRange(oldMessages);
				}
			}
			catch (Exception err)
			{
				Console.WriteLine("Error reading old messages: " + err.Message);
			}

			// Time used to quick set time intervals between tweets.
			int minuteSleepMin = 5;
			int minuteSleepMax = 30;

			// Load all the people that Taylor Swift follows.
			LoadTaylorsFollowing();

			// Infinite loop, lel
			while (true)
			{
				// Choose what to do.
				int actionToTake = _rand.Next(0, 10);

				if (actionToTake < 5)
				{
					// Tweet to someone Taylor Swift follows.
					TweetToFriend();
				}
				else
				{
					// Tweets to Taylor Swift.
					TweetToTaylorSwift13();
				}

				// Pick a random amount of time, turn it into minutes in miliseonds
				int minutes = _rand.Next(minuteSleepMin, minuteSleepMax);
				int sleepTime = minutes * 60 * 1000;
				Console.WriteLine("Sleeping for " + minutes + " minutes.");
				Thread.Sleep(sleepTime);
			}

			// Still in from debugging, is used so the screen wont disapear.
			Console.ReadLine();
		}

		// Fills _friends with _friendsList.Users. When we tweet to a friend we remove them from this list and then
		// rebuild it when it is empty. Reason being we want to send an even number of tweets to all of her friends.
		private static void UpdateFriendsToSendList()
		{
			if (_friendsList == null)
				return;


			_friends.Clear();
			foreach (TTTTUsers user in _friendsList.Users)
			{
				_friends.Add(user.ScreenName);
			}

		}

		// Loads people Taylor Swift follows, in twitter terms, "Friends"
		private static void LoadTaylorsFollowing()
		{
			try
			{
				// Does the list exist? If so load it and then update it.
				// If the friends list json is found it won't update from the server. I don't expect Taylor to start
				// following too many people over this duration so I figured I'd just load a cached copy.
				string friendsJson = String.Empty;
				if (_friendsList == null && File.Exists("friends.json"))
				{
					friendsJson = File.ReadAllText("friends.json");
					_friendsList = JsonConvert.DeserializeObject<TTTTFriendsListResponse>(friendsJson);
					UpdateFriendsToSendList();

					return;

				}

				// Fill in your own token stuff for your twitter app.
				var oauth_token = "<FILL IN YOUR OWN>";
				var oauth_token_secret = "<FILL IN YOUR OWN>";
				var oauth_consumer_key = "<FILL IN YOUR OWN>";
				var oauth_consumer_secret = "<FILL IN YOUR OWN>";

				var oAuthConsumerKey = oauth_consumer_key;
				var oAuthConsumerSecret = oauth_consumer_secret;
				var oAuthUrl = "https://api.twitter.com/oauth2/token";
				var screenname = "taylorswift13";

				// Do the Authenticate
				var authHeaderFormat = "Basic {0}";

				// Authenticate.
				var authHeader = string.Format(authHeaderFormat,
					Convert.ToBase64String(Encoding.UTF8.GetBytes(Uri.EscapeDataString(oAuthConsumerKey) + ":" +
					Uri.EscapeDataString((oAuthConsumerSecret)))
				));

				var postBody = "grant_type=client_credentials";

				HttpWebRequest authRequest = (HttpWebRequest)WebRequest.Create(oAuthUrl);
				authRequest.Headers.Add("Authorization", authHeader);
				authRequest.Method = "POST";
				authRequest.ContentType = "application/x-www-form-urlencoded;charset=UTF-8";
				authRequest.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

				using (Stream stream = authRequest.GetRequestStream())
				{
					byte[] content = ASCIIEncoding.ASCII.GetBytes(postBody);
					stream.Write(content, 0, content.Length);
				}

				authRequest.Headers.Add("Accept-Encoding", "gzip");

				WebResponse authResponse = authRequest.GetResponse();

				// deserialize into an object
				TwitAuthenticateResponse twitAuthResponse;
				using (authResponse)
				{
					using (var reader = new StreamReader(authResponse.GetResponseStream()))
					{
						//JavaScriptSerializer js = new JavaScriptSerializer();
						var objectText = reader.ReadToEnd();
						twitAuthResponse = JsonConvert.DeserializeObject<TwitAuthenticateResponse>(objectText);
					}
				}

				// Load twitter friends list (people the user follows)
				// https://dev.twitter.com/rest/reference/get/friends/list
				// We don't bother iterating over this to get all friends as there is less than 200.
				var timelineFormat = "https://api.twitter.com/1.1/friends/list.json?screen_name={0}&skip_status=1&count=200&";
				var timelineUrl = string.Format(timelineFormat, screenname);
				HttpWebRequest timeLineRequest = (HttpWebRequest)WebRequest.Create(timelineUrl);
				var timelineHeaderFormat = "{0} {1}";
				timeLineRequest.Headers.Add("Authorization", string.Format(timelineHeaderFormat, twitAuthResponse.token_type, twitAuthResponse.access_token));
				timeLineRequest.Method = "Get";
				WebResponse timeLineResponse = timeLineRequest.GetResponse();
				friendsJson = string.Empty;
				using (timeLineResponse)
				{
					using (var reader = new StreamReader(timeLineResponse.GetResponseStream()))
					{
						friendsJson = reader.ReadToEnd();
					}
				}


				// Store the list in memory.
				_friendsList = JsonConvert.DeserializeObject<TTTTFriendsListResponse>(friendsJson);
				UpdateFriendsToSendList();

				// Save it out to file for use later.
				File.WriteAllText("friends.json", friendsJson);

			}
			catch (Exception err)
			{
				Console.WriteLine("Error: " + err.Message);
			}
		}

		// When tweeting to a friend, this is used to generate the message.
		private static string GetFriendMessage()
		{
			string message = String.Empty;
			string screenName = String.Empty;
			int toSendTo = -1;


			do
			{
				// If we used all the friends up, re-fill the list.
				if (_friends.Count == 0)
				{
					UpdateFriendsToSendList();
				}

				// Pick a random person to tweet to.
				toSendTo = _rand.Next(0, _friends.Count);
				screenName = _friends[toSendTo];


				// Used to pick a random word from to keep the message less copy/paste for each tweet.
				string[] heyWords = new string[] { "Hey", "Hi", "G'day", "Hello", "Hey", "Hi", "Hello", "Hey", "Hi", "Hello", "Hey", "Hi", "Hello" };

				// Used to pick a random word from to keep the message less copy/paste for each tweet.
				string[] secondWords = new string[] {
					"my gfs 21st ",
					"my girlfriends 21st ",
					"my girlfriends 21st party ",
					"my gfs 21st party ",
					"my gfs 21st bday ",
					"my girlfriends 21st bday ",
					"my girlfriends 21st bday party ",
					"my gfs 21st bday party ",
					"my gfs 21st birthday ",
					"my girlfriends 21st birthday ",
					"my girlfriends 21st birthday party ",
					"my gfs 21st birthday party "
			};

				// Format the message and who it is going to here.
				message = String.Format("{0} @{1}, I'm trying to get @taylorswift13 to {2}(or even a birthday tweet), do you think you can help me out?", heyWords[_rand.Next(0, heyWords.Length)], screenName, secondWords[_rand.Next(0, secondWords.Length)]);

				// If the tweet is too long we try again to get a shorter combination.
			} while (message.Length > 140);

			// Remove this person from the friends list.
			_friends.RemoveAt(toSendTo);

			return message;
		}

		// Tweets to one of Taylors friends.
		private static void TweetToFriend()
		{
			// In case for some reason this has not been loaded.
			if (_friendsList == null)
			{
				LoadTaylorsFollowing();
				return;
			}

			// Generate random message.
			string message = GetFriendMessage();

			// Send it out.
			SendTweet(message);
		}

		// When tweeting to a Taylor, this is used to generate the message.
		private static string GenerateMessage()
		{
			// lol, thats me!
			//return "@beeradmoore test " + _rand.Next(0, 100);

			StringBuilder sb = new StringBuilder();

			// Decied tweet format.
			if (_rand.Next(0, 2) == 0)
			{
				// .@taylorswift13 <greeting> <name>
				sb.Append(".@taylorswift13 ");

				// Decide if we want to include a greeting.
				if (_rand.Next(0, 5) > 0)
				{
					// Bunch of random greetings and name uses. Used to make Tweet a lot less spammy.
					string[] heyWords = new string[] { "Hey", "Hi", "G'day", "Hello", "Hey", "Hi", "Hello", "Hey", "Hi", "Hello", "Hey", "Hi", "Hello" };
					sb.Append(heyWords[_rand.Next(0, heyWords.Length)] + " ");

					string[] nameWords = new string[] { "Taylor", "Taytay", "Taylor", "Taylor" };
					sb.Append(nameWords[_rand.Next(0, nameWords.Length)] + ", ");
				}
			}
			else
			{
				// <greeting> @taylorswift13

				// Pick a random greeting
				string[] heyWords = new string[] { "Hey", "Hi", "G'day", "Hello", "Hey", "Hi", "Hello", "Hey", "Hi", "Hello", "Hey", "Hi", "Hello" };
				sb.Append(heyWords[_rand.Next(0, heyWords.Length)] + " ");

				sb.Append("@taylorswift13, ");
			}

			// Pick a message. 
			// Always remember kids, I before E except after C
			// https://twitter.com/spelIingerror/status/569585691664850944
			// Not awkward at all after 2.5K tweets.
			string[] firstWords = new string[] {
					"have not recieved your RSVP to ",
					"please RSVP to ",
					"would you like to come to ",
					"please come to ",
					"you're invited to ",
					"you're invited to ",
					"you're invited to ",
					"it'd be amazing if you could come to ",
					"it'd be great if you could come to ",
					"come to ",
					"make a guest appearance at ",
					"did not recieve your RSVP to ",
			};
			sb.Append(firstWords[_rand.Next(0, firstWords.Length)]);

			// Pick a second message, again to increase the difference in each message.
			string[] secondWords = new string[] {
					"my gfs 21st, ",
					"my girlfriends 21st, ",
					"my girlfriends 21st party, ",
					"my gfs 21st party, ",
					"my gfs 21st bday, ",
					"my girlfriends 21st bday, ",
					"my girlfriends 21st bday party, ",
					"my gfs 21st bday party, ",
					"my gfs 21st birthday, ",
					"my girlfriends 21st birthday, ",
					"my girlfriends 21st birthday party, ",
					"my gfs 21st birthday party, ",
			};
			sb.Append(secondWords[_rand.Next(0, secondWords.Length)]);

			// Always include the date.
			sb.Append("28th Feb");

			// Choose what way to say Melbourne.
			if (_rand.Next(0, 2) == 0)
			{
				sb.Append(", Melbourne");
			}
			else
			{
				sb.Append(", Melb");
			}

			// Choose what way to say Australia.
			if (_rand.Next(0, 2) == 0)
			{
				sb.Append(", Australia");
			}
			else
			{
				sb.Append(", Aus");
			}

			// Hash tag that is added to all tweets to taylor, not to friends (they were too long to fit this in)
			sb.Append(" #taylorPlzRespond");
			return sb.ToString();
		}

		// Tweets to Taylor Swift.
		private static void TweetToTaylorSwift13()
		{
			string message = String.Empty;

			// Pick a random message. GetFriendMessage does the 140 character trick within it but 
			// I do it here for this one. No idea why its just how I wrote it at the time.
			do
			{
				message = GenerateMessage();
			} while (message.Length > 140);

			SendTweet(message);
		}

		private static void SendTweet(string message)
		{
			try
			{
				// Fill in your apps token data.
				OAuthTokens credentials = new OAuthTokens();
				credentials.AccessToken = "<FILL IN YOUR OWN>";
				credentials.AccessTokenSecret = "<FILL IN YOUR OWN>";
				credentials.ConsumerKey = "<FILL IN YOUR OWN>";
				credentials.ConsumerSecret = "<FILL IN YOUR OWN>";

				// Be sure to use the 1.1 API.
				StatusUpdateOptions options = new StatusUpdateOptions();
				options.APIBaseAddress = "https://api.twitter.com/1.1/";

				// Send the tweet and display the output of when it was sent.
				var response = TwitterStatus.Update(credentials, message, options);
				StringBuilder sb = new StringBuilder();
				sb.AppendLine("DateTime: " + DateTime.Now.ToString());

				// If the tweet was sucessful
				if (response.Result == RequestResult.Success)
				{
					// Display complete URL of the tweet as well as the tweet text itself.
					sb.AppendLine("Result: Success");
					sb.AppendLine("https://twitter.com/taylorplsrespnd/status/" + response.ResponseObject.Id);
					sb.AppendLine(message);

					// Fill it in a MessageObject for storing.
					MessageObject messageObject = new MessageObject()
					{
						ID = GetMD5(message),
						TwitterID = response.ResponseObject.Id.ToString(),
						Message = message,
						Sent = DateTime.Now,
					};

					// Add it to our saved lists.
					_sentMessages.Add(messageObject);

					/*
					if (File.Exists("sent.json"))
					{
						File.Move("sent.json", "sent_bu.json");
					}
					 * */

					// Write the entire list out to sent.json, could be optimized but that is not needed here.
					File.WriteAllText("sent.json", JsonConvert.SerializeObject(_sentMessages));


				}
				else
				{
					// Tweet was not successful for some reason.
					sb.AppendLine("Result: " + response.Result.ToString() + " (" + response.ErrorMessage + ")");
				}


				// Display the result of all this we just got and also log it to file.
				Console.WriteLine(sb.ToString());

				sb.AppendLine("");
				sb.AppendLine("");


				File.AppendAllText("result.log", sb.ToString());
			}
			catch (Exception err)
			{
				Console.WriteLine("Error: " + err.Message);
			}
		}


		// Gets a MD5 of the tweeted message. No particular reason, just a unique (or close enough too) ID.
		private static string GetMD5(string inputString)
		{
			MD5 md5 = System.Security.Cryptography.MD5.Create();
			byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(inputString);
			byte[] hash = md5.ComputeHash(inputBytes);

			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < hash.Length; ++i)
			{
				sb.Append(hash[i].ToString("X2"));
			}

			return sb.ToString();
		}
	}
}
