using System;
using System.Collections;
using LTC.Identity;
using UnityEngine;
using UnityEngine.Networking;

// Read-only, player-authorized endpoint. No admin credentials or arbitrary player IDs.
public static class LTCStatisticsCloud
{
    [Serializable] public class GameCount { public string gameCode; public int completed; }
    [Serializable] public class Point { public string date; public float score; }
    [Serializable] public class Cohort {
        public string status; public int ageMin,ageMax,minimumPlayers,players;
        public bool hasScore; public float score,median,percentile; public int[] bins;
    }
    [Serializable] public class Response {
        public int days,completed,activeDays7; public string gameCode,startDate,endDate,dateBasis;
        public GameCount[] games; public Point[] trend; public Cohort cohort;
    }
    public static readonly string[] Codes={"STP","ORD","SUM","CRD","PIP","SUP","QIZ","GOP"};
    public static IEnumerator Fetch(int days,int game,Action<Response,string> completed)
    {
        var identity=PlayerIdentityService.Current;
        if(!identity.IsReady || string.IsNullOrEmpty(identity.AccessToken)){completed(null,"尚未完成玩家登入，請稍後重新整理。");yield break;}
        string owner=identity.PlayerId,token=identity.AccessToken;
        string baseUrl=PlayerPrefs.GetString(PlayerIdentityService.ApiBaseUrlPlayerPrefsKey,"https://staging-hello-8shi.encr.app").Trim().TrimEnd('/');
        string url=baseUrl+"/api/v1/player/statistics?days="+days+"&gameCode="+(game<0?"":Codes[game]);
        using(var request=UnityWebRequest.Get(url)){
            request.SetRequestHeader("Authorization","Bearer "+token);request.timeout=15;
            yield return request.SendWebRequest();
            if(identity.PlayerId!=owner || identity.AccessToken!=token){completed(null,"帳號已變更，請重新整理。");yield break;}
            if(request.result!=UnityWebRequest.Result.Success){
                string message=request.responseCode==401?"登入已過期，請重新登入。":request.responseCode==404?"雲端統計接口尚未部署。":"暫時無法取得雲端資料，請重新整理。";
                completed(null,message);yield break;
            }
            Response value=null;try{value=JsonUtility.FromJson<Response>(request.downloadHandler.text);}catch(Exception){ }
            if(value==null||value.games==null||value.trend==null||value.cohort==null||value.days!=days){completed(null,"雲端統計格式不完整，請稍後重試。");yield break;}
            completed(value,null);
        }
    }
}
