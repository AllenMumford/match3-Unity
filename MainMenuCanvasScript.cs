using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System;

public class MainMenuCanvasScript : MonoBehaviour
{

    public GameObject gameOverTextObject;



    // Start is called before the first frame update
    void Start()
    {
        /* Logic: check PlayerPrefs's CurrentPlayerName. If set, that means that a game just ended and the "Game Over! Score: ..." text should be displayed.
         * If CurrentPlayerName is not set, this should be the bootup of the game, rather than the return to the menu screen after game over.
         */

        //Check the CurrentPlayerName
        string CurrentPlayerName = PlayerPrefs.GetString("CurrentPlayerName", null);

        if (String.IsNullOrEmpty(CurrentPlayerName))
        {
            //CurrentPlayerName is empty/null
            //Set "Game Over" text to off
            gameOverTextObject.SetActive(false);

        }
        else
        {
            //CurrentPlayerName is NOT empty/null
            //Set "Game Over" message to "Game Over! Score: [NUMBER]"
            gameOverTextObject.GetComponent<Text>().text = "Game Over! Score: " + PlayerPrefs.GetInt("CurrentPlayerScore", 0) + ", Moves: " + PlayerPrefs.GetInt("CurrentPlayerMoves", 0);

            //Set "Game Over" text to on
            gameOverTextObject.SetActive(true);

            //Reset CurrentPlayerName and CurrentPlayerScore and moves
            PlayerPrefs.SetString("CurrentPlayerName", "");
            PlayerPrefs.SetInt("CurrentPlayerScore", 0);
            PlayerPrefs.SetInt("CurrentPlayerMoves", 0);

        }

    }

    // Update is called once per frame
    void Update()
    {
        
    }

    //Called by the Play button
    public void PlayGame()
    {
        //Load the Gameplay scene
        SceneManager.LoadScene(1);
    }
}
