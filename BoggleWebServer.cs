// Last modiefied 4/15/16 by Spenser DuBois 5:30:00 pm.
// Created 4/10/16, Authors Spenser DuBois and Aaryn GoodWill



using CustomNetworking;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.ServiceModel.Web;
using System.Text;
using System.Text.RegularExpressions;

namespace Boggle
{
    /// <summary>
    /// Represents a basic web server for playing a game of boggle.
    /// AUTHORS: Spenser DuBois, Aaryn Goodwill
    /// </summary>
    public class BoggleWebServer
    {
        public static void Main()
        {
            new BoggleWebServer();
            Console.Read();
        }

        private TcpListener server;
        private BoggleService service;
        public static HttpStatusCode StatusCode { get; set; }
        public BoggleWebServer()
        {
            server = new TcpListener(IPAddress.Any, 60000);
            server.Start();
            server.BeginAcceptSocket(ConnectionRequested, null);
            service = new BoggleService();
        }

        private void ConnectionRequested(IAsyncResult ar)
        {
            Socket s = server.EndAcceptSocket(ar);
            server.BeginAcceptSocket(ConnectionRequested, null);
            new HttpRequest(new StringSocket(s, new UTF8Encoding()), service);
        }
    }

    class HttpRequest
    {
        /// <summary>
        /// Represents the string socket for this particular HTTP request.
        /// </summary>
        private StringSocket ss;

        /// <summary>
        /// The number of lines a request contains
        /// </summary>
        private int lineCount;

        /// <summary>
        /// Represents the number of characters in the request. This is recieved from header information.
        /// </summary>
        private int contentLength;

        /// <summary>
        /// Represents the type of method being call. ie PUSH, POST, GET
        /// </summary>
        private string methodName = null;

        /// <summary>
        /// Represents the URL and method type from a given request.
        /// </summary>
        private String method, url;

        /// <summary>
        /// The status of the response back from the DB.
        /// </summary>
        public static HttpStatusCode code;

        /// <summary>
        /// Represents the boggle service that the request is being sent about.
        /// </summary>
        private BoggleService service;

        /// <summary>
        /// Represents the game ID, if any, for a given request.
        /// </summary>
        private string gameID = "";


        public HttpRequest(StringSocket stringSocket, BoggleService s)
        {
            this.ss = stringSocket;
            this.service = s;
            ss.BeginReceive(LineReceived, null);
        }

        private void SendErrorResponse()
        {
            ss.BeginSend("HTTP/1.1 400 BadRequest\r\n", (ex, py) => { ss.Shutdown(); }, null);
        }

        /// <summary>
        /// Recieves a line from the response and get the information needed out of it.
        /// </summary>
        private void LineReceived(string s, Exception e, object payload)
        {
            lineCount++;
            Console.WriteLine(s);
            if (s != null)
            {

                if (lineCount == 1)
                {
                    Regex r = new Regex(@"^(\S+)\s+(\S+)");
                    Match m = r.Match(s);

                    //Retrieve method
                    method = m.Groups[1].Value;
                    Console.WriteLine("Method: " + m.Groups[1].Value);

                    //Retrieve url
                    url = m.Groups[2].Value;
                    Console.WriteLine("URL: " + m.Groups[2].Value);

                    //need to check if method is a GET and whether to take a part URL
                    string gameID = null;
                    string brief = null;
                    
                    if (method.Equals("GET") && (new Regex(@"(\/\w+\.\w+\/\w+\/\d+\?\w+\=\w+)").IsMatch(url) || new Regex(@"(\/\w+\.\w+\/\w+\/\d+)").IsMatch(url)))
                    {
                        url = SplitURL(url, out gameID, out brief);
                        Console.WriteLine(url);
                    }
                    //make sure method matches a signature in the interface
                    if (!IsMethod(method, url, out methodName))
                    {
                        SendErrorResponse();
                        return;
                    }

                    //since GET /games doesn't have a content length and we have all of the info we
                    //need from the url, execute the method here.
                    if (methodName.Equals("GameStatus"))
                    {
                        GameStatusRequestRecieved(gameID, brief);
                        return;
                    }
                }
                if (s.StartsWith("Content-Length:"))
                {
                    contentLength = Int32.Parse(s.Substring(16).Trim());
                }
                if (s == "\r")
                {
                    //Begin receiving the body of the request and execute appropriate method
                    //determined by methodName.
                    //BeginReceive(ss, contentLength, methodName);
                    ss.BeginReceive(ContentReceived, null, contentLength);
                }
                else
                {
                    ss.BeginReceive(LineReceived, null);
                }
            }
        }

        /// <summary>
        /// When a game status request is recieved, processes the request.
        /// </summary>
        /// <param name="gameID"></param>
        /// <param name="brief"></param>
        private void GameStatusRequestRecieved(string gameID, string brief)
        {
            Game gameStatus = service.GameStatus(gameID, brief);

            string result =
                    JsonConvert.SerializeObject(gameStatus,
                            new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            ss.BeginSend("HTTP/1.1 " + (int)service.statusCode + " " + service.statusCode + " \r\n", Ignore, null);
            ss.BeginSend("Content-Type: application/json\r\n", Ignore, null);
            ss.BeginSend("Content-Length: " + result.Length + "\r\n", Ignore, null);
            ss.BeginSend("\r\n", Ignore, null);
            ss.BeginSend(result, (ex, py) => { ss.Shutdown(); }, null);
        }


        /// <summary>
        /// Returns a bool based on whether the received method is in the interface.
        /// Outputs a methodName parameter that is the name literal of the method.
        /// </summary>
        /// <param name="method"></param>
        /// <param name="url"></param>
        /// <returns></returns>
        private bool IsMethod(string method, string url, out string methodName)
        { 
            
            switch (method)
            {
                case "POST":
                    if (url.Equals("/BoggleService.svc/users"))
                    {
                        methodName = "CreateUser";
                        return true;
                    }
                    if (url.Equals("/BoggleService.svc/games"))
                    {
                        methodName = "JoinGame";
                        return true;
                    }
                    break;
                case "PUT":
                      

                    string[] split = url.Split('/');
                    if (split[split.Length - 1] != "games")
                    {
                        gameID = split[split.Length - 1];
                        methodName = "PlayWord";
                        return true;
                    }
                    if (url.Equals("/BoggleService.svc/games"))
                    {
                        methodName = "CancelJoinRequest";
                        return true;
                    }
                    break;
                case "GET":
                    if (url.Contains("/BoggleService.svc/games"))
                    {
                        methodName = "GameStatus";
                        return true;
                    }
                    break;
                default:
                    methodName = null;
                    return false;
            }
            methodName = null;
            return false;
        }
        /// <summary>
        /// Takes the gameID and brief status from the GET url and returns the first portion.
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private string SplitURL(string url, out string gameID, out string brief)
        {

            Regex r = new Regex(@"(\d+).?(brief.(\w+))?");
            Match m = r.Match(url);

            gameID = m.Groups[1].Value;
            brief = m.Groups[3].Value;

            return url.Remove(24, m.ToString().Length+1);
        }

        /// <summary>
        /// When all the contents from a request has been recieved, we process the information and send it back to the client
        /// </summary>
        /// <param name="s"></param>
        /// <param name="e"></param>
        /// <param name="payload"></param>
        private void ContentReceived(string s, Exception e, object payload)
        {
            if (s != null)
            {
                // The boggle service we will be using and passing into the method below.
                //IBoggleService boggle = new BoggleService();

                // result is what we get back from passing the information to our DB.
                string result = GetObject(s);

                // Figured out how to get the status code to work properly. Had to append the code number and name to the HTTP/1.1 response.
                //ss.BeginSend("HTTP/1.1 " + (int)code + " " + code.ToString() + "\r\n", Ignore, null);
                ss.BeginSend("HTTP/1.1 " + (int)service.statusCode + " " +service.statusCode +  " \r\n", Ignore, null);
                ss.BeginSend("Content-Type: application/json" + "\r\n", Ignore, null);
                ss.BeginSend("Content-Length: " + result.Length + "\r\n", Ignore, null);
                ss.BeginSend("\r\n", Ignore, null);
                ss.BeginSend(result, (ex, py) => { ss.Shutdown(); }, null);
            }
        }

        /// <summary>
        /// Determines how to recieve the incoming request and what type of object needs to be created.
        /// </summary>
        /// <param name="ss"></param>
        /// <param name="contentLength"></param>
        /// <param name="methodName"></param>
        private string GetObject(String s)
        {
            switch (methodName)
            {
                // Used for creating a user.
                case "CreateUser":
                    UserInfo user = JsonConvert.DeserializeObject<UserInfo>(s);
                    UserTokenObject token = service.CreateUser(user);
                    string result =
                    JsonConvert.SerializeObject(token,
                            new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                    return result;
                // Used for joing a game.
                case "JoinGame":
                    JoinGameInfo info = JsonConvert.DeserializeObject<JoinGameInfo>(s);
                    GameiD id = service.JoinGame(info);
                    string gID = JsonConvert.SerializeObject(id,
                            new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                    return gID;
                // Used for cancelling a join request.
                case "CancelJoinRequest":
                    Cancel cancel = JsonConvert.DeserializeObject<Cancel>(s);
                    service.CancelJoinRequest(cancel);
                    return "";
                
                // Used for playing a word
                case "PlayWord":
                    WordCheck word = JsonConvert.DeserializeObject<WordCheck>(s);
                    WordScore score = service.PlayWord(gameID, word);
                    string wordPlayed =
                    JsonConvert.SerializeObject(score,
                            new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

                    return wordPlayed;
                default:
                    return "";
            }
        }

        private void Ignore(Exception e, object payload)
        {
        }
    }
}