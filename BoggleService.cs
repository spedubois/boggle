// Last modiefied 4/15/16 by Spenser DuBois 5:30:00 pm.
// Created 4/10/16, Authors Spenser DuBois and Aaryn GoodWill

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Net;
using System.ServiceModel.Web;
using System.Text.RegularExpressions;
using System.Web;
using static System.Net.HttpStatusCode;

namespace Boggle
{


    public class BoggleService : IBoggleService
    {
        private static string BoggleDB;
        private static Dictionary<string, string> dictionary = new Dictionary<string, string>();
        public HttpStatusCode statusCode;

        public BoggleService()
        {
            InitializeDictionary();
            BoggleDB = ConfigurationManager.ConnectionStrings["BoggleDB"].ConnectionString;
        }

        private void InitializeDictionary()
        {
            string fileName = "..\\dictionary.txt";

            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
            using (TextReader read = new StreamReader(path))
            {
                string line;
                string exit;
                while ((line = read.ReadLine()) != null)
                {
                    if (dictionary.TryGetValue(line, out exit))
                    {
                        return;
                    }
                    dictionary.Add(line, RetrieveScore(line).ToString());
                }
            }
        }
        /// <summary>
        /// The most recent call to SetStatus determines the response code used when
        /// an http response is sent.
        /// </summary>
        /// <param name="status"></param>
        private void SetStatus(HttpStatusCode status)
        {
            statusCode = status;
        }

        public HttpStatusCode getStatus()
        {
            return statusCode;
        }

        /// <summary>
        /// Returns a Stream version of index.html.
        /// </summary>
        /// <returns></returns>
        public Stream API()
        {
            SetStatus(OK);
            WebOperationContext.Current.OutgoingResponse.ContentType = "text/html";
            return File.OpenRead(AppDomain.CurrentDomain.BaseDirectory + "index.html");
        }

        public Game GameStatus(string gameID, string brief)
        {
            try {
                if (brief == null || brief == "")
                {
                    brief = "no";
                }
                string status;
                DateTime time;
                int TimeLimit;

                if(!(new Regex(@"\d+").IsMatch(gameID)))
                {
                    SetStatus(Forbidden);
                    return null;
                }
                //Creates a sql connection
                using (SqlConnection conn = new SqlConnection(BoggleDB))
                {
                    //open connection
                    conn.Open();
                    using (SqlTransaction trans = conn.BeginTransaction())
                    {
                        // Gets the status, start time and time limit of the game.
                        using (SqlCommand command = new SqlCommand("select GameStatus, StartTime, TimeLimit from Games where GameID = @GameID",
                            conn, trans))
                        {
                            command.Parameters.AddWithValue("@GameID", gameID);

                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                //added check to make sure game was in table
                                if (!reader.HasRows)
                                {
                                    SetStatus(Forbidden);
                                    return null;
                                }

                                reader.Read();
                                status = reader.GetString(0);

                                // If the game is still pending, Just returns a Game object with Pending as the status.
                                if (status == "pending")
                                {
                                    SetStatus(OK);
                                    return new Game { GameState = status };
                                }

                                // else we get the time the game started and the calculated time limit from the 2 players.
                                time = reader.GetDateTime(1);
                                TimeLimit = (int)reader.GetValue(2);
                            }
                        }

                        // Calculates how much time has passed since the game became Active. Rounds the number to a whole number.
                        double elapsedTime = Math.Round((DateTime.Now.TimeOfDay.TotalSeconds - time.TimeOfDay.TotalSeconds), 0);

                        // If the elapsed time is greater than or equal to the time limit, the game is marked as completed so no 
                        // Actions can be made to the game.
                        if (elapsedTime >= (double)TimeLimit || status == "completed")
                        {
                            using (SqlCommand command1 = new SqlCommand("Update Games set GameStatus = @GameStatus where GameID = @GameID",
                        conn, trans))
                            {
                                status = "completed";
                                command1.Parameters.AddWithValue("@GameStatus", status);
                                command1.Parameters.AddWithValue("@GameID", gameID);

                            }

                        }

                        trans.Commit();

                        // GenerateGameStatus is a helper method that will generate a Game object depending on the status of a given game.
                        return GenerateGameStatus(conn, trans, status, brief, gameID, elapsedTime);

                        
                    }
                    
                }
            }
            catch (Exception)
            {
                SetStatus(Forbidden);
                return null;
            }
        }

        /// <summary>
        /// Registers a player.
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public UserTokenObject CreateUser(UserInfo user)
        {

            if (user.Nickname == null || user.Nickname.Trim().Length == 0 || user.Nickname.Length > 50)
            {
                SetStatus(Forbidden);
                return null;
            }

            //Creates a sql connection
            using (SqlConnection conn = new SqlConnection(BoggleDB))
            {
                //open connection
                conn.Open();
                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    using (SqlCommand command = new SqlCommand("insert into Users (UserID, Nickname) values (@UserID, @Nickname)",
                        conn, trans))
                    {
                        //create usertoken
                        string userID = Guid.NewGuid().ToString();

                        //add placeholder values
                        command.Parameters.AddWithValue("UserID", userID);
                        command.Parameters.AddWithValue("@Nickname", user.Nickname);

                        //make sure only 1 row was changed
                        if (command.ExecuteNonQuery() == 0)
                        {
                            SetStatus(Forbidden);
                            return null;
                        }
                        SetStatus(Created);

                        //comit transaction and return the usertoken
                        trans.Commit();
                        return new UserTokenObject { UserToken = userID };
                    }
                }
            }
        }

        /// <summary>
        /// Play a word in a game
        /// 
        /// If Word is null or empty when trimmed, or if GameID or 
        /// UserToken is missing or invalid, or if UserToken is not a
        /// player in the game identified by GameID, responds with response 
        /// code 403 (Forbidden).
        /// 
        /// Otherwise, if the game state is anything other than "active", 
        /// responds with response code 409 (Conflict).
        /// 
        /// Otherwise, records the trimmed Word as being played by UserToken
        /// in the game identified by GameID. Returns the score for Word in the 
        /// context of the game (e.g. if Word has been played before the score is zero).
        /// Responds with status 200 (OK). Note: The word is not case sensitive.
        /// </summary>
        /// <param name="gameID"></param>
        /// <param name="UserToken"></param>
        /// <param name="Word"></param>
        /// <returns></returns>
        public WordScore PlayWord(string gameID, WordCheck info)
        {
            try {
                string word = info.Word.Trim();
                string userToken = info.UserToken;
                BoggleBoard PlayBoard;
                string status1;
                int score = 0;
                bool canPlay;
                DateTime time;
                int TimeLimit;

                if (word.Length == 0 || userToken == null || gameID == null)
                {
                    SetStatus(Forbidden);
                    return null;
                }

                if(!(new Regex(@"\d+").IsMatch(gameID)))
                {
                    SetStatus(Forbidden);
                    return null;
                }
                //Creates a sql connection
                using (SqlConnection conn = new SqlConnection(BoggleDB))
                {
                    //open connection
                    conn.Open();

                    using (SqlTransaction trans = conn.BeginTransaction())
                    {
                        using (SqlCommand command = new SqlCommand("select GameStatus, StartTime, TimeLimit from Games where GameID = @GameID",
                           conn, trans))
                        {
                            command.Parameters.AddWithValue("@GameID", gameID);

                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                //added check to make sure game was in table
                                if (!reader.HasRows)
                                {
                                    SetStatus(Forbidden);
                                    return null;
                                }

                                reader.Read();
                                status1 = reader.GetString(0);

                                // else we get the time the game started and the calculated time limit from the 2 players.
                                time = reader.GetDateTime(1);
                                TimeLimit = (int)reader.GetValue(2);

                                double elapsedTime = Math.Round((DateTime.Now.TimeOfDay.TotalSeconds - time.TimeOfDay.TotalSeconds), 0);

                                // If the elapsed time is greater than or equal to the time limit, the game is marked as completed so no 
                                // Actions can be made to the game.
                                if (elapsedTime >= (double)TimeLimit)
                                {
                                    SetStatus(Conflict);
                                    status1 = "completed";
                                }
                            }
                        }
                    }
                    using (SqlTransaction trans = conn.BeginTransaction())
                    {
                        // Gets thge players and board based on the given GameID.
                        using (SqlCommand command = new SqlCommand("select Player1, Player2, Board, GameStatus from Games where GameID = @GameID",
                            conn, trans))
                        {
                            string P1;
                            string P2;
                            string status;
                            command.Parameters.AddWithValue("@GameID", gameID);
                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                if (!reader.HasRows)
                                {
                                    SetStatus(Forbidden);
                                    return null;
                                }

                                reader.Read();

                                P1 = reader.GetString(0);
                                P2 = reader.GetString(1);
                                status = reader.GetString(3);
                                
                                if(!P1.Equals(userToken) && !P2.Equals(userToken))
                                {
                                    SetStatus(Forbidden);
                                    return null;
                                }
                                if (status1 != "active")
                                {
                                    SetStatus(Conflict);
                                    return null;
                                }
                                PlayBoard = new BoggleBoard(reader.GetString(2));
                                // Checks to make sure P1 and P2 are in the game.
                                if (P1 == null || P2 == null)
                                {
                                    SetStatus(Forbidden);
                                    return null;
                                }

                                // If game status is Active, sets a bool that allows up to play a word. If it is anything but Active,
                                // we can't play a word.
                                if (status == "active")
                                {
                                    canPlay = true;
                                }
                                else
                                {
                                    canPlay = false;
                                    SetStatus(Conflict);
                                }
                            }
                        }

                        // If we can play a word, we go in to the if statement. If not, we set the status to 409 - Conflict and return null
                        if (canPlay)
                        {
                            // Checks to make sure the word can be formed on the game board.
                            if (PlayBoard.CanBeFormed(word))
                            {
                                string scoreString;
                                // Sets the score initially to the word score from the dictionary. Can be changed below if the word has already been played by
                                // the player.
                                if (dictionary.TryGetValue(word.ToUpper(), out scoreString))
                                {
                                    int.TryParse(scoreString, out score);
                                    // Gets all the words played by a player in a game.
                                    using (SqlCommand command2 = new SqlCommand("select Word from Words where GameID = @GameID and Player = @Player",
                                            conn, trans))
                                    {
                                        command2.Parameters.AddWithValue("@GameID", gameID);
                                        command2.Parameters.AddWithValue("@Player", userToken);
                                        using (SqlDataReader reader1 = command2.ExecuteReader())
                                        {
                                            // Reads each row/word that a player has played for a game.
                                            while (reader1.Read())
                                            {
                                                // If the word has been played, score is updated to 0.
                                                if (reader1.GetString(0) == word)
                                                {
                                                    score = 0;
                                                    break;
                                                }
                                            }
                                        }
                                    }

                                }
                            }
                            // If the word can't be formed on the board or is an invalid word, score is set to -1.
                            else
                            {
                                score = -1;
                            }

                            // Inserts the word into the Words table. The word is accociated with a GameID, Player, and score.
                            using (SqlCommand command3 = new SqlCommand("Insert into Words (Word, GameID, Player, Score) values (@Word, @GameID, @Player, @Score)",
                                            conn, trans))
                            {
                                command3.Parameters.AddWithValue("@Word", word);
                                command3.Parameters.AddWithValue("@GameID", gameID);
                                command3.Parameters.AddWithValue("@Player", userToken);
                                command3.Parameters.AddWithValue("@Score", score);

                                if (command3.ExecuteNonQuery() != 1)
                                {
                                    SetStatus(BadRequest);
                                    return null;
                                }
                            }


                            SetStatus(OK);

                            //comit transaction and return the usertoken
                            trans.Commit();
                            // Returns a WordScore object that reflect the score of the word a player just played.
                            return new WordScore { Score = score.ToString() };
                        }
                        // Only gets done when the game status is anything other than Active.

                        {
                            SetStatus(Conflict);
                            return null;
                        }
                    }
                }
            }
            catch (Exception)
            {
                SetStatus(Conflict);
                return null;
            }
        }
        /// <summary>
        /// Join a game
        /// 
        /// If UserToken is invalid, TimeLimit < 
        /// 5, or TimeLimit > 120, responds with 
        /// status 403 (Forbidden).
        /// 
        /// Otherwise, if UserToken is already a
        /// player in the pending game, responds 
        /// with status 409 (Conflict).
        /// 
        /// Otherwise, if there is already one player
        /// in the pending game, adds UserToken as the
        /// second player. The pending game becomes active
        /// and a new pending game with no players is 
        /// created. The active game's time limit is the 
        /// integer average of the time limits requested by
        /// the two players. Returns the new active game's 
        /// GameID (which should be the same as the old pending
        /// game's GameID). Responds with status 201 (Created).
        /// 
        /// Otherwise, adds UserToken as the first player of the
        /// pending game, and the TimeLimit as the pending game's 
        /// requested time limit. Returns the pending game's GameID.
        /// Responds with status 202 (Accepted).
        /// </summary>
        /// <param name="UserToken"></param>
        /// <param name="TimeLimit"></param>
        /// <returns></returns>
        public GameiD JoinGame(JoinGameInfo user)
        {
            try {
                GameiD id = new GameiD();
                if (user.UserToken == null)
                {
                    SetStatus(Forbidden);
                    return null;
                }

                if (user.TimeLimit < 5 || user.TimeLimit > 120)
                {
                    SetStatus(Forbidden);
                    return null;
                }

                using (SqlConnection conn = new SqlConnection(BoggleDB))
                {
                    conn.Open();

                    using (SqlTransaction trans = conn.BeginTransaction())
                    {
                        int time = 0;
                        int gameID = 0;
                        //Check for a pending game
                        using (SqlCommand command = new SqlCommand("select GameID, Player1, TimeLimit from Games where Player2 is null",
                            conn, trans))
                        {

                            //reads, checks for a pending game by seeing if there are rows returned.
                            //if there aren't any rows returned, that means there are no pending games
                            //so we create one with the player1 and timelimit added.
                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                //make sure a row was returned, if not means there is no
                                //pending game with a first player, create one.
                                if (!reader.HasRows)
                                {
                                    //close reader - error if we don't
                                    reader.Close();

                                    //create a new game with the player and get the auto generated
                                    //gameID back
                                    id.GameID = CreatePendingGame(user, conn, trans);

                                    //commit transaction and return id
                                    trans.Commit();
                                    SetStatus(Accepted);
                                    return id;
                                }

                                //read the first null game and get the first players time request
                                //as well as that games ID
                                if (reader.Read())
                                {
                                    time = reader.GetInt32(2);
                                    gameID = reader.GetInt32(0);
                                }

                                //check if player is already in the game...
                                if (reader.GetString(1).Equals(user.UserToken))
                                {
                                    SetStatus(Conflict);
                                    return null;
                                }
                            }
                        }


                        //Since we have the first players time request and game id we update the database
                        //with the second player updated time, a new board and the current time.
                        using (SqlCommand command = new SqlCommand("update Games set Player2 = @Player2, TimeLimit = @TimeLimit, Board = @Board, StartTime = @StartTime, GameStatus = @GameStatus where GameID = @GameID",
                            conn, trans))
                        {
                            command.Parameters.AddWithValue("@Player2", user.UserToken);
                            command.Parameters.AddWithValue("@TimeLimit", (time + user.TimeLimit) / 2);
                            command.Parameters.AddWithValue("@Board", new BoggleBoard().ToString());
                            command.Parameters.AddWithValue("@StartTime", DateTime.Now.TimeOfDay);
                            command.Parameters.AddWithValue("@GameID", gameID);
                            command.Parameters.AddWithValue("@GameStatus", "active");

                            //if there were no rows affected then I set status to badrequest
                            //so there is no continuation
                            if (command.ExecuteNonQuery() == 0)
                            {
                                SetStatus(BadRequest);
                                return null;
                            }
                        }

                        //Otherwise sets status
                        id.GameID = gameID.ToString();
                        SetStatus(Created);
                        trans.Commit();
                        return id;
                    }
                }
            }
            catch (Exception)
            {
                SetStatus(Forbidden);
                return null;
            }
        }
        //Creates a new pending game with the user as the first player
        public string CreatePendingGame(JoinGameInfo user, SqlConnection conn, SqlTransaction trans)
        {
            using (SqlCommand command = new SqlCommand("insert into Games (Player1, TimeLimit, GameStatus) output inserted.GameID values (@Player1, @TimeLimit, @GameStatus)",
                conn, trans))
            {
                command.Parameters.AddWithValue("@Player1", user.UserToken);
                command.Parameters.AddWithValue("@TimeLimit", user.TimeLimit);
                command.Parameters.AddWithValue("@GameStatus", "pending");

                string s = command.ExecuteScalar().ToString();
                return s;
            }
        }


        /// <summary>
        /// Helper method that generates a Game object specific to the status of a game, and if brief was supplied in the URL or not.
        /// </summary>
        public Game GenerateGameStatus(SqlConnection conn, SqlTransaction trans, string status, string brief, string GameID, double elapsedTime)
        {
            // This method is a little messy and there is a lot going on but everything appears to be working correctly.


            UserInfo player1 = new UserInfo();
            UserInfo player2 = new UserInfo();
            // These are the ID's for the players of a game
            string p1ID;
            string p2ID;
            // The temporary game we will be returning.
            Game temp = new Game();
            string board;
            int timeLimit;
            double timeRemaing;
            string P1Nickname;
            string P2Nickname;

            // Initializing the words played for ewach player
            player1.WordsPlayed = new List<Words>();
            player2.WordsPlayed = new List<Words>();


            // To start, we pull the players, borad, and time limit of a game. we set the variables above to these values.
            using (SqlCommand command = new SqlCommand("select Player1, Player2, Board, TimeLimit from Games where GameID = @GameID",
               conn, trans))
            {
                command.Parameters.AddWithValue("@GameID", GameID);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    reader.Read();

                    p1ID = reader.GetString(0);
                    p2ID = reader.GetString(1);
                    board = reader.GetString(2);
                    timeLimit = (int)reader.GetValue(3);
                    timeRemaing = timeLimit - elapsedTime;
                }

            }

            // Not very good at writing queries so had to make multipul for similar operations. This one and the one
            // below it just get the nicknames for each player
            using (SqlCommand command = new SqlCommand("select Nickname from Users where UserID = @P1",
               conn, trans))
            {
                command.Parameters.AddWithValue("@P1", p1ID);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    reader.Read();

                    P1Nickname = reader.GetString(0);
                }

            }

            using (SqlCommand command = new SqlCommand("select Nickname from Users where UserID = @P2",
               conn, trans))
            {
                command.Parameters.AddWithValue("@P2", p2ID);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    reader.Read();

                    P2Nickname = reader.GetString(0);
                }

            }


            // The following 2 queries generate the list of words and their scores that the player has played. 
            // This first one is for player 1, and the next one is for player 2.
            using (SqlCommand command = new SqlCommand("select Word, Score from Words where GameID = @GameID and Player = @Player",
              conn, trans))
            {
                command.Parameters.AddWithValue("@GameID", GameID);
                command.Parameters.AddWithValue("@Player", p1ID);

                using (SqlDataReader reader = command.ExecuteReader())
                {

                    int score = 0;
                    while (reader.Read())
                    {
                        // create a new Word object
                        Words word = new Words();
                        //Set the word and score
                        word.Word = reader.GetString(0);
                        word.Score = (int)reader.GetValue(1);
                        //add the word to player 1's list of words.
                        player1.WordsPlayed.Add(word);
                        // Update the players overall score.
                        score += word.Score;
                    }
                    player1.Score = score;
                }

            }

            // This is for player 2.
            using (SqlCommand command = new SqlCommand("select Word, Score from Words where GameID = @GameID and Player = @Player",
             conn, trans))
            {
                command.Parameters.AddWithValue("@GameID", GameID);
                command.Parameters.AddWithValue("@Player", p2ID);

                using (SqlDataReader reader = command.ExecuteReader())
                {

                    int score = 0;
                    while (reader.Read())
                    {
                        Words word = new Words();
                        word.Word = reader.GetString(0);
                        word.Score = (int)reader.GetValue(1);
                        player2.WordsPlayed.Add(word);
                        score += word.Score;
                    }
                    player2.Score = score;
                }

            }

            // Once we have calculated and gathered everything, we check the game status and if brief was yes or not.
            if (status == "active")
            {
                if (brief.ToLower() == "yes")
                {
                    // Since brief was "yes", we don't return the words so we set the words played by the players to null.
                    player1.WordsPlayed = null;
                    player2.WordsPlayed = null;
                    temp.GameState = status;
                    temp.TimeLeft = timeRemaing;
                    temp.Player1 = player1;
                    temp.Player2 = player2;
                    // This is where we return the appropriate Game object for the game status.
                    return temp;
                }

                // If brief was null or anything other than "yes", we return a more detailed Game object.
                else
                {
                    // Words are still null for an Active game.
                    player1.WordsPlayed = null;
                    player2.WordsPlayed = null;
                    player1.Nickname = P1Nickname;
                    player2.Nickname = P2Nickname;

                    temp.Board = board;
                    temp.TimeLimit = timeLimit;
                    temp.GameState = status;
                    temp.TimeLeft = timeRemaing;
                    temp.Player1 = player1;
                    temp.Player2 = player2;
                    return temp;
                }
            }

            // Almost the exact same as for Active, only we DO return words when brief is null of anything other than "yes".
            if (status == "completed")
            {
                if (brief.ToLower() == "yes")
                {
                    player1.WordsPlayed = null;
                    player2.WordsPlayed = null;
                    temp.GameState = status;
                    temp.TimeLeft = 0;
                    temp.Player1 = player1;
                    temp.Player2 = player2;
                    return temp;
                }

                else
                {
                    player1.Nickname = P1Nickname;
                    player2.Nickname = P2Nickname;

                    temp.Board = board;
                    temp.TimeLimit = timeLimit;
                    temp.GameState = status;
                    temp.TimeLeft = 0;
                    temp.Player1 = player1;
                    temp.Player2 = player2;
                    return temp;
                }

            }
            return null;
        }
        public void CancelJoinRequest(Cancel user)
        {
            if (user.UserToken == null)
            {
                SetStatus(Forbidden);
                return;
            }

            using (SqlConnection conn = new SqlConnection(BoggleDB))
            {
                conn.Open();
                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    using (SqlCommand command = new SqlCommand("delete from Games where GameID in (select GameID from Games where GameStatus = @GameStatus and Player1 = @UserToken)",
                        conn, trans))
                    {
                        command.Parameters.AddWithValue("@UserToken", user.UserToken);
                        command.Parameters.AddWithValue("@GameStatus", "pending");

                        if (command.ExecuteNonQuery() == 0)
                        {
                            SetStatus(Forbidden);
                            return;
                        }
                        else
                        {
                            trans.Commit();
                            SetStatus(OK);
                            return;
                        }
                    }
                }
            }
        }

        public int RetrieveScore(string word)
        {
            int score = 1;
            if (word.Length < 3)
            {
                return 0;
            }
            for (int i = 0; i <= word.Length; i++)
            {
                if (i > 4 && i < 7)
                {
                    score++;
                }
                if (i == 7)
                    score += 2;
                if (i > 7)
                {
                    return 11;
                }
            }
            return score;
        }

    }
}
