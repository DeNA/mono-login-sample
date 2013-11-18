using System.Collections;
using System.Collections.Generic;

using LitJson;

namespace Mobage {
	public enum CancelableAPIStatus {
		CancelableAPIStatusSuccess,
		CancelableAPIStatusCancel,
		CancelableAPIStatusError
	}
	
	public enum SimpleAPIStatus {
		SimpleAPIStatusSuccess,
		SimpleAPIStatusError
	}
	
	public enum DismissableAPIStatus {
	}
	
	public class Error {
		public int code;
		public string description;
		public string localizedDescription;

		public Error(string description) {
			this.code = 0;
			this.description = description;
			localizedDescription = description;
		}
	}

	public enum ServerEnvironment {
		Production=0,
		Sandbox=1
	}

	public class Mobage {
		// Initialization variables.
		public static ServerEnvironment environment;

		public static string GetEnvironmentUrl(string appId) {
			switch(environment) {
			case ServerEnvironment.Production:
				return "https://app.mobage.com/1/" + appId;
			case ServerEnvironment.Sandbox:
				return "http://app-sandbox.mobage.com/1/" + appId;
			default:
				return "invalid environment";
			}
		}
	}

	public class GameClient {
		public static string appId;
		public static string consumerKey;
		public static string consumerSecret;

		// Session variables.
		public string oauthToken;
		public string oauthTokenSecret;

		public delegate void executeLogin_onCompleteCallback(CancelableAPIStatus status, Error error);
		public void Login(string username, string password, executeLogin_onCompleteCallback cb) {
			string url = Mobage.GetEnvironmentUrl(GameClient.appId) + "/session";
			NetworkQueue.Request req = new NetworkQueue.Request(url, "POST");
			req.headers.Add("Accept", "application/json");
			req.body = "gamertag=" + username +
				"&password=" + password +
				"&id=012d521f-bc7f-40de-9124-2aa99b9bd334" +
				"&device_type=iPhone" +
				"&os_version=3.0" +
				"&local=en";
			req.profile = false;
			req.trace = false;

			req.Callback(delegate(NetworkQueue.Response res) {
				if(res.error != null) {
					cb(0, new Error(res.error));
					return;
				}
				
				JsonData json = JsonMapper.ToObject(res.bodyString);
				
				if(!json.Contains("success") || !json.Contains("oauth_token") || !json.Contains("oauth_secret")) {
					cb(0, new Error("Missing fields from server response"));
					return;
				}
				
				if(json["success"].GetBoolean() != true) {
					cb(0, new Error("Success is false"));
					return;
				}

				this.oauthToken = json["oauth_token"].GetString();
				this.oauthTokenSecret = json["oauth_secret"].GetString();

				cb(0, null);
			});

			NetworkQueue net = NetworkQueue.instance;
			net.Enqueue(req);
		}

		public delegate void authorizeToken_onCompleteCallback(SimpleAPIStatus status, Error error, string verifier);
		public void authorizeToken(string token, authorizeToken_onCompleteCallback cb) {
			string url = Mobage.GetEnvironmentUrl(GameClient.appId) + "/oauth/authorize";
			NetworkQueue.Request req = new NetworkQueue.Request(url, "GET");
			req.OAuth(GameClient.consumerKey, GameClient.consumerSecret, this.oauthToken, this.oauthTokenSecret);
			req.body = "authorize=1&id=012d521f-bc7f-40de-9124-2aa99b9bd334&oauth_token=" + token;
			req.trace = false;

			req.Callback(delegate(NetworkQueue.Response res) {
				if(res.error != null) {
					cb(0, new Error(res.error), null);
					return;
				}

				JsonData json = JsonMapper.ToObject(res.bodyString);
	
				if(!json.Contains("success") || !json.Contains("oauth_verifier")) {
					cb(0, new Error("Missing fields from server response"), null);
					return;
				}

				if(json["success"].GetBoolean() != true) {
					cb(0, new Error("Success is false"), null);
					return;
				}

				cb(0, null, json["oauth_verifier"].GetString());
			});

			NetworkQueue.instance.Enqueue(req);
		}
	}

	public class GameServer {
		public static string appId;
		public static string consumerKey;
		public static string consumerSecret;

		// Session variables.
		public string oauthToken;
		public string oauthTokenSecret;

		public delegate void requestTempToken_onCompleteCallback(SimpleAPIStatus status, Error error);
		public void requestTempToken(requestTempToken_onCompleteCallback cb) {
			string url = Mobage.GetEnvironmentUrl(GameServer.appId) + "/request_temporary_credential";
			NetworkQueue.Request req = new NetworkQueue.Request(url, "GET");
			req.OAuth(GameServer.consumerKey, GameServer.consumerSecret, null, null);
			req.body = "id=012d521f-bc7f-40de-9124-2aa99b9bd334";
			req.trace= false;

			req.Callback(delegate(NetworkQueue.Response res) {
				if(res.error != null) {
					cb(0, new Error(res.error));
					return;
				}

				if (res.headers["Content-Type"] != "application/x-www-form-urlencoded") {
					cb(0, new Error("Invalid content-type: " + res.headers["Content-Type"]));
					return;
				}
	
				this.parseResponse(res);

				if(this.oauthToken == null || this.oauthTokenSecret == null) {
					cb(0, new Error("Missing fields from server response"));
					return;
				}

				cb(0, null);
			});

			NetworkQueue.instance.Enqueue(req);
		}

		public void requestToken(requestTempToken_onCompleteCallback cb) {
			string url = Mobage.GetEnvironmentUrl(GameServer.appId) + "/request_token";
			NetworkQueue.Request req = new NetworkQueue.Request(url, "GET");
			req.OAuth(GameServer.consumerKey, GameServer.consumerSecret, this.oauthToken, this.oauthTokenSecret);
			req.body = "id=012d521f-bc7f-40de-9124-2aa99b9bd334";
			req.trace= false;

			req.Callback(delegate(NetworkQueue.Response res) {
				if(res.error != null) {
					cb(0, new Error(res.error));
					return;
				}

				if (res.headers["Content-Type"] != "application/x-www-form-urlencoded") {
					cb(0, new Error("Invalid content-type: " + res.headers["Content-Type"]));
					return;
				}
	
				this.parseResponse(res);

				if(this.oauthToken == null || this.oauthTokenSecret == null) {
					cb(0, new Error("Missing fields from server response"));
					return;
				}

				cb(0, null);
			});

			NetworkQueue.instance.Enqueue(req);
		}

		private void parseResponse (NetworkQueue.Response res)
		{
			this.oauthToken = null;
			this.oauthTokenSecret = null;

			string[] kv1 = res.bodyString.Split ('&');
			string[] kv2;
			foreach (string kv in kv1) {
				kv2 = kv.Split ('=');
				if (kv2.Length == 2) {
					if (kv2 [0] == "oauth_token") {
						this.oauthToken = kv2 [1];
					} else if (kv2 [0] == "oauth_token_secret") {
						this.oauthTokenSecret = kv2 [1];
					}
				}
			}
		}
	}
}