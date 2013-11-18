using System;
using System.Threading;

namespace mobagedemomono
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			Console.WriteLine("Start");
			Mobage.Mobage.environment = Mobage.ServerEnvironment.Sandbox;
			NetworkQueue.instance.Start();
			Mobage.GameClient.appId = "";
			Mobage.GameClient.consumerKey = "";
			Mobage.GameClient.consumerSecret = "";
			Mobage.GameServer.appId = "";
			Mobage.GameServer.consumerKey = "";
			Mobage.GameServer.consumerSecret = "";
			MainClass mc = new MainClass();
			mc.start ();
			Console.WriteLine("End");
		}

		private bool wait = true;
		private Mobage.GameClient gc;
		private Mobage.GameServer gs;

		private MainClass() {
			this.gc = new Mobage.GameClient();
			this.gs = new Mobage.GameServer();
		}

		public void start ()
		{
			Console.WriteLine ("User Name:");
			String username = Console.ReadLine();
			Console.WriteLine ("Password:");
			String password = Console.ReadLine();

			this.gc.Login (username, password, this.loginCB);
			while (wait) {
				Thread.Sleep (1);
			}
		}

		public void loginCB (Mobage.CancelableAPIStatus status, Mobage.Error error)
		{
			if (status == Mobage.CancelableAPIStatus.CancelableAPIStatusSuccess) {
				Console.WriteLine ("Login Success");
				this.reqTempToken();
			} else if (status == Mobage.CancelableAPIStatus.CancelableAPIStatusCancel) {
				Console.WriteLine ("Login Cancelled");
				wait = false;
			} else if (status == Mobage.CancelableAPIStatus.CancelableAPIStatusError) {
				Console.WriteLine ("Login Error {0}: {1}", error.code, error.description);
				wait = false;
			}
		}

		public void reqTempToken ()
		{
			this.gs.requestTempToken (this.reqTempTokenCB);
		}

		public void reqTempTokenCB (Mobage.SimpleAPIStatus status, Mobage.Error error)
		{
			if (error == null) {
				Console.WriteLine ("Temporary Token Success");
				this.authorize (this.gs.oauthToken);
			} else {
				Console.WriteLine ("Temporary Token Failed: {0} - {1}", error.code, error.description);
				wait = false;
			}
		}

		public void authorize (string token)
		{
			this.gc.authorizeToken (token, authorizeCB);
		}

		public void authorizeCB (Mobage.SimpleAPIStatus status, Mobage.Error error, string verifier)
		{
			if (status == Mobage.SimpleAPIStatus.SimpleAPIStatusSuccess) {
				Console.WriteLine ("Authorize Token Success");
				this.reqToken ();
			} else {
				Console.WriteLine ("Authorize Token Failed: {0} - {1}", error.code, error.description);
				wait = false;
			}
		}

		public void reqToken ()
		{
			this.gs.requestToken (this.reqTokenCB);
		}

		public void reqTokenCB (Mobage.SimpleAPIStatus status, Mobage.Error error)
		{
			if (status == Mobage.SimpleAPIStatus.SimpleAPIStatusSuccess) {
				Console.WriteLine ("Request Token Success");
			} else {
				Console.WriteLine ("Request Token Failed: {0} - {1}", error.code, error.description);
			}
			wait = false;
		}
	}
}
