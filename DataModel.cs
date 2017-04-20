// Last modiefied 4/15/16 by Spenser DuBois 5:30:00 pm.
// Created 4/10/16, Authors Spenser DuBois and Aaryn GoodWill

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Boggle
{
    /// <summary>
    /// Represents a player on the server.
    /// </summary>
    [DataContract]
    public class UserInfo
    {
        [DataMember(EmitDefaultValue = false)]
        public string Nickname { get; set; }
        [DataMember(EmitDefaultValue = false)]
        public int? Score { get; set; }
        [IgnoreDataMember]
        public List<Words> WordsPlayedDefault { get; set; }
        [DataMember(EmitDefaultValue = false)]
        public List<Words> WordsPlayed { get; set; }
        [IgnoreDataMember]
        public string UserToken { get; set; }
    }
    public class UserInfoBrief
    {

    }

    [DataContract]
    public class UserTokenObject
    {
        [DataMember]
        public string UserToken { get; set; }
    }
    public class Words
    {
        public string Word { get; set; }
        public int Score { get; set; }
    }
    [DataContract]
    public class WordCheck
    {
        [DataMember]
        public String UserToken { get; set; }
        [DataMember]
        public string Word { get; set; }
        [DataMember]
        public string GameID { get; set; }
    }
    [DataContract]
    public class WordScore
    {
        [DataMember]
        public String Score { get; set; }
    }
    //Join game info
    [DataContract]
    public class JoinGameInfo
    {
        [DataMember]
        public string UserToken { get; set; }
        [DataMember]
        public int TimeLimit { get; set; }
    }
    /// <summary>
    /// Represents a game that has been played. cointains the words, scores, time and players of that game.
    /// </summary>
    [DataContract]
    public class Game
    {
        [DataMember]
        public string GameState { get; set; }
        [DataMember(EmitDefaultValue = false)]
        public string Board { get; set; }
        [DataMember(EmitDefaultValue = false)]
        public double? TimeLimit { get; set; }
        [DataMember(EmitDefaultValue = false)]
        public double? TimeLeft { get; set; }
        [DataMember(EmitDefaultValue = false)]
        public UserInfo Player1 { get; set; }
        [DataMember(EmitDefaultValue = false)]
        public UserInfo Player2 { get; set; }
        [IgnoreDataMember]
        public double Time;
    }

    public class GameiD
    {
        public string GameID { get; set; }
    }

    [DataContract]
    public class Cancel
    {
        [DataMember]
        public string UserToken { get; set; }
    }

}
