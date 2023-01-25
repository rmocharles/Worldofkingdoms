using System.Collections;
using System.Collections.Generic;
using BackEnd;
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/*
 * LoginSceneManager에 들어갈 기능
 *
 * 1. 신규 유저 확인
 * 2. 닉네임 확인
 * 3. 유저 정보 확인
 */

public class LoginSceneManager : Singleton<LoginSceneManager>
{
    [SerializeField] private Button startButton;
    [SerializeField] private TMP_Text copyRightText;
    [SerializeField] private TMP_Text versionText;
    [SerializeField] private GameObject loginButtonGroup;
    void Awake()
    {
        //StaticManager가 없을 경우 새로 생성
        if (FindObjectOfType(typeof(StaticManager)) == null)
        {
            var obj = Resources.Load<GameObject>("Prefabs/StaticManager");
            Instantiate(obj);
        }
        
#if UNITY_ANDROID || UNITY_IOS
        Application.targetFrameRate = 120;
#endif
    }

    void Start()
    {
        #region GPGS 플러그인 설정

#if UNITY_ANDROID
        // GPGS 플러그인 설정
        PlayGamesClientConfiguration config = new PlayGamesClientConfiguration
                .Builder()
            .RequestServerAuthCode(false)
            .RequestEmail()
            .RequestIdToken()
            .Build();
        //커스텀 된 정보로 GPGS 초기화
        PlayGamesPlatform.InitializeInstance(config);
        PlayGamesPlatform.DebugLogEnabled = true; // 디버그 로그를 보고 싶지 않다면 false
        PlayGamesPlatform.Activate();
#endif

        #endregion

        startButton.gameObject.SetActive(false);
        loginButtonGroup.gameObject.SetActive(false);
        copyRightText.gameObject.SetActive(false);
        versionText.gameObject.SetActive(false);
        
        if (Application.internetReachability == NetworkReachability.NotReachable)
            StaticManager.UI.AlertUI.OpenUI("네트워크에 연결이 되어 있지 않습니다.\n앱을 다시 실행해주세요.", () =>
            {
                Application.Quit();
            });
        
        startButton.onClick.AddListener(() =>
        {
            startButton.gameObject.SetActive(false);
            loginButtonGroup.SetActive(true);
        });

        StartCoroutine(Initialize());
    }

    public IEnumerator Initialize()
    {
        //언어 대응 불러오기
        StaticManager.Langauge.Initialize();

        yield return new WaitUntil(() => StaticManager.Langauge.isLoading);
        
        copyRightText.gameObject.SetActive(true);
        versionText.text = "Version : " + Application.version;
        versionText.gameObject.SetActive(true);
        
        //로그인 버튼 기능 삽입
        Button[] loginButtons = loginButtonGroup.GetComponentsInChildren<Button>();
        loginButtons[0].onClick.AddListener(() => BackendLogin("Google"));
        loginButtons[1].onClick.AddListener(() => BackendLogin("Apple"));
        loginButtons[2].onClick.AddListener(() => BackendLogin("Guest"));
        loginButtons[3].onClick.AddListener(() => BackendLogin("Test"));

#if UNITY_ANDROID
        loginButtons[1].gameObject.SetActive(false);
        
#elif UNITY_IOS
        loginButtons[0].gameObject.SetActive(false);
#endif
        
        //테스트용 로그인 버튼 (출시할시 지울 것)
        //loginButtonGroup.transform.GetChild(3).gameObject.SetActive(false);

        AutoLoginWithBackend();
    }
    
    //PolicyUI 뒤로가기 할시 호출
    public void ActiveLoginGroup()
    {
        if (loginButtonGroup.activeSelf) return;
        
        startButton.gameObject.SetActive(false);
        loginButtonGroup.SetActive(true);
    }
    
    /* 자동 로그인 함수 호출
     * 신규 유저 : 회원가입 버튼 활성화
     * 기존 유저 : 자동 로그인
     */
    private void AutoLoginWithBackend()
    {
        SendQueue.Enqueue(Backend.BMember.LoginWithTheBackendToken, callback =>
        {
            Debug.LogWarning($"Backend.BMember.LoginWithTheBackendToken : {callback}");

            //로그인 성공했을 경우(토큰 O)
            if (callback.IsSuccess())
            {

                //닉네임이 없을 경우
                if (string.IsNullOrEmpty(Backend.UserNickName))
                {
                    //정책 UI 활성화
                    loginButtonGroup.SetActive(false);
                    //GetPolicy();
                }

                //닉네임이 있을 경우
                else
                {
                    // InitializeLoading();
                }
            }

            //최초 로그인일 경우
            else
            {
                startButton.gameObject.SetActive(true);
            }
        });
    }
    
    /* 뒤끝 로그인 기능. 이후 처리는 AuthorizeProcess 참고 */
    private void BackendLogin(string platform)
    {
        StaticManager.UI.SetLoading(true);

        switch (platform)
        {
            case "Google":
                if (Social.localUser.authenticated)
                {
                    var token = GetFederationToken();
                    if (token.Equals(string.Empty))
                    {
                        StaticManager.UI.SetLoading(false);

                        Debug.LogError("GPGS 토큰이 존재하지 않습니다.");
                        StaticManager.UI.AlertUI.OpenUI("GPGS 토큰이 존재하지 않습니다.");
                        return;
                    }

                    SendQueue.Enqueue(Backend.BMember.AuthorizeFederation, token, FederationType.Google, AuthorizeProcess);
                }
                else
                {
                    Social.localUser.Authenticate((bool success) =>
                    {
                        if (success)
                        {
                            var token = GetFederationToken();
                            if (token.Equals(string.Empty))
                            {
                                StaticManager.UI.SetLoading(false);

                                Debug.LogError("GPGS 토큰이 존재하지 않습니다.");
                                StaticManager.UI.AlertUI.OpenUI("GPGS 토큰이 존재하지 않습니다.");
                                return;
                            }

                            SendQueue.Enqueue(Backend.BMember.AuthorizeFederation, token, FederationType.Google, AuthorizeProcess);
                        }
                        else
                        {
                            StaticManager.UI.SetLoading(false);
                            Debug.LogError("GPGS 토큰이 존재하지 않습니다2.");
                            StaticManager.UI.AlertUI.OpenUI("GPGS 토큰이 존재하지 않습니다2.\n" + success.ToString());
                        }
                    });
                }
                break;
            
            case "Apple":
                break;
            
            case "Guest":
                SendQueue.Enqueue(Backend.BMember.GuestLogin, AuthorizeProcess);
                break;
            
            case "Test":
                SendQueue.Enqueue(Backend.BMember.CustomSignUp, "SandboxTest_" + Random.Range(0, 10000), "1234", AuthorizeProcess);
                break;
        }
    }

    #region 구글 토큰

    private string GetFederationToken()
    {
#if UNITY_ANDROID
        if (!PlayGamesPlatform.Instance.localUser.authenticated)
        {
            Debug.LogError("GPGS에 접속되어 있지 않습니다.");
            return string.Empty;
        }

        string _IDtoken = PlayGamesPlatform.Instance.GetIdToken();
        return _IDtoken;
#endif
    }

    #endregion
    
    /*
     * 로그인 함수 호출 후 처리하는 함수
     */
    private void AuthorizeProcess(BackendReturnObject callback)
    {
        StaticManager.UI.SetLoading(false);
        Debug.LogWarning($"Backend.BMember.AuthroizeProcess : {callback}");
        
        if (!callback.IsSuccess())
        {
            if(callback.GetStatusCode() == "401")
            {
                string id = Backend.BMember.GetGuestID();

                if (!string.IsNullOrEmpty(id))
                {
                    Debug.Log("로컬 기기에 저장된 아이디 :" + id);
                    Backend.BMember.DeleteGuestInfo();
                    SendQueue.Enqueue(Backend.BMember.GuestLogin, AuthorizeProcess);
                }
            }
            else
            {
                loginButtonGroup.SetActive(true);
            }
            return;
        }

        //닉네임이 없을 경우
        if (string.IsNullOrEmpty(Backend.UserNickName))
        {
            loginButtonGroup.SetActive(false);
            //GetPolicy();
            return;
        }

        //새로 가입인 경우에는 StatusCode가 201, 기존 로그인일 경우에는 200이 리턴
        if (callback.GetStatusCode() == "201")
        {
            loginButtonGroup.SetActive(false);
            //GetPolicy();
        }
        else
        {
            //로딩 불러오기
            //InitializeLoading();
        }
    }
}
