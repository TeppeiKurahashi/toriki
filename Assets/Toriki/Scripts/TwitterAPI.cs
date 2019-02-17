﻿using UnityEngine;
using System;
using System.Collections.Generic;
using Toriki.Settings;
using Toriki.Internal;

namespace Toriki
{
    public class TwitterAPI : MonoBehaviour
    {
        [Serializable]
        private class SessionData
        {
            [SerializeField] public string nickname;
            [SerializeField] public string accessToken;
            [SerializeField] public string accessSecret;
        }

        [Serializable]
        private class ErrorData
        {
            [SerializeField] public int code;
            [SerializeField] public string message;
        }

        [Serializable]
        public class APIError
        {
            [Serializable]
            public class Error
            {
                [SerializeField]
                public int code;

                [SerializeField]
                public string message;
            }

            [SerializeField]
            public Error[] errors;
        }

        private TwitterConnector _connector;

        private Action<string, string, string> loginSuccessAction { set; get; }
        private Action<int, string> loginFailureAction { set; get; }

        /*
            called from Native Plugin via UnitySendMessage.
         */
        public void LoginComplete(string sessionData)
        {
            if (loginSuccessAction != null)
            {
                var session = JsonUtility.FromJson<SessionData>(sessionData);
                loginSuccessAction(session.nickname, session.accessToken, session.accessSecret);
            }
        }

        /*
            called from Native Plugin via UnitySendMessage.
         */
        public void LoginFailed(string errorData)
        {
            if (loginFailureAction != null)
            {
                var error = JsonUtility.FromJson<ErrorData>(errorData);
                loginFailureAction(error.code, error.message);
            }
        }

        /*
            static functions.
         */

        private enum TKState
        {
            NotLoggedIn,
            APIReady
        }
        private static TKState _state = TKState.NotLoggedIn;

        private static ITwitterImpl _twitter;
        private static TwitterAPI _component;

        public static void AwakeInit()
        {
            var twitterGameObject = new GameObject("TorikiGameObject");
            _component = twitterGameObject.AddComponent<TwitterAPI>();

#if UNITY_EDITOR
            _twitter = new EditorTwitterImpl();
#elif UNITY_IOS
            _twitter = new IOSTwitterImpl();
#elif UNITY_ANDROID
            _twitter = new AndroidTwitterImpl();
#endif
        }

        public static void Init()
        {
            if (_twitter == null)
            {
                TwitterAPI.AwakeInit();
                _twitter.Init(TwitterSettings.ConsumerKey, TwitterSettings.ConsumerSecret);
            }
        }

        public static void InitWithToken(string accessToken, string accessSecret)
        {
            Init();

            // initialize twitter connector if accessToken and secret is not null.
            _component._connector = new TwitterConnector(TwitterSettings.ConsumerKey, TwitterSettings.ConsumerSecret, accessToken, accessSecret);
            _state = TKState.APIReady;
        }

        public static void LogIn(Action<string, string, string> successCallback, Action<int, string> failureCallback)
        {
            _component.loginSuccessAction = (nickname, token, secret) =>
            {
                // API Connectorの初期化
                _component._connector = new TwitterConnector(TwitterSettings.ConsumerKey, TwitterSettings.ConsumerSecret, token, secret);
                _state = TKState.APIReady;
                successCallback(nickname, token, secret);
            };
            _component.loginFailureAction = failureCallback;
            _twitter.LogIn();
        }

        public static void Get(string url, SortedDictionary<string, string> parameters, Action<string> onSucceeded, Action<int, APIError.Error[]> onFailed)
        {
            if (_state == TKState.NotLoggedIn)
            {
                onFailed(0, new APIError.Error[]{new APIError.Error(){
                    code = 0,
                    message = "not logged in. please Login first."
                }});
                return;
            }

            var cor = _component._connector.GenerateAccessCoroutine(
                url,
                TKMethod.GET,
                parameters,
                null,
                (conId, code, resp, data) =>
                {
                    onSucceeded(data);
                },
                (conId, code, error, resp) =>
                {
                    onFailed(code, error.errors);
                }
            );

            _component.StartCoroutine(cor);
        }

        public static void Post(string url, SortedDictionary<string, string> parameters, Action<string> onSucceeded, Action<int, APIError.Error[]> onFailed)
        {
            if (_state == TKState.NotLoggedIn)
            {
                onFailed(0, new APIError.Error[]{new APIError.Error(){
                    code = 0,
                    message = "not logged in. please Login first."
                }});
                return;
            }

            var cor = _component._connector.GenerateAccessCoroutine(
                url,
                TKMethod.POST,
                null,
                parameters,
                (conId, code, resp, data) =>
                {
                    onSucceeded(data);
                },
                (conId, code, error, resp) =>
                {
                    onFailed(code, error.errors);
                }
            );

            _component.StartCoroutine(cor);
        }
    }
}
