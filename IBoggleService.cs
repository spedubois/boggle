// Last modiefied 4/15/16 by Spenser DuBois 5:30:00 pm.
// Created 4/10/16, Authors Spenser DuBois and Aaryn GoodWill

using System;
using System.Collections.Generic;
using System.IO;
using System.ServiceModel;
using System.ServiceModel.Web;

namespace Boggle
{
    [ServiceContract]
    public interface IBoggleService
    {
        /// <summary>
        /// Sends back index.html as the response body.
        /// </summary>
        [WebGet(UriTemplate = "/api")]
        Stream API();

        /// <summary>
        /// Creates new user
        /// 
        /// If Nickname is null, or is empty when trimmed, responds with status 403 (Forbidden).
        /// 
        /// Otherwise, creates a new user with a unique UserToken and the trimmed Nickname. 
        /// The returned UserToken should be used to identify the user in subsequent requests.
        /// Responds with status 201 (Created).
        /// </summary>
        /// <param name="nickName"></param>
        /// <returns></returns>
        [WebInvoke(Method = "POST", UriTemplate = "/users")]
        UserTokenObject CreateUser(UserInfo name);

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
        [WebInvoke(Method = "POST", UriTemplate = "/games")]
        GameiD JoinGame(JoinGameInfo userInfo);

        /// <summary>
        /// Cancel a peding request to join a game
        /// 
        /// If UserToken is invalid or is not a player in the pending
        /// game, responds with status 403 (Forbidden).
        /// 
        /// Otherwise, removes UserToken from the pending game and 
        /// responds with status 200 (OK).
        /// </summary>
        /// <param name="UserToken"></param>
        [WebInvoke(Method = "PUT", UriTemplate = "/games")]
        void CancelJoinRequest(Cancel user);

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
        [WebInvoke(Method = "PUT", UriTemplate = "/games/{gameID}")]
        WordScore PlayWord(string gameID, WordCheck info);

        /// <summary>
        /// Get game status information
        /// 
        /// If GameID is invalid, responds with status 403 (Forbidden).
        /// 
        /// Otherwise, returns information about the game named by GameID 
        /// as illustrated below. Note that the information returned depends
        /// on whether "Brief=yes" was included as a parameter as well as on
        /// the state of the game. Responds with status code 200 (OK). Note: 
        /// The Board and Words are not case sensitive.

        /// </summary>
        /// <param name="gameID"></param>
        /// <param name="brief"></param>
        //<returns></returns>
        [WebGet(UriTemplate = "/games/{gameID}?Brief={brief}")]
        Game GameStatus(string gameID, string brief);
    }
}
