using UnityEngine;
using UnityEngine.UI;

public class TokenSelectedScript : MonoBehaviour
{
    public int xOnBoard;
    public int yOnBoard;
    public GameObject gameManager;
    public bool selected;
    public Color CurrentlyUnselected;
    public Color CurrentlySelected;

    void Start()
    {
        selected = false;
    }

    public void TokenSelected()
    {
        GameManagerScript gmScript = gameManager.GetComponent<GameManagerScript>();
        gmScript.InformTokenSelected(this.gameObject, xOnBoard, yOnBoard);
    }

    public void setSelected(bool sel)
    {
        //Set color and bool val
        if (sel)
        {
            this.GetComponent<Image>().color = CurrentlySelected;
            selected = true;
            this.GetComponent<Animator>().SetBool("IsSelected", true);
        } else
        {
            this.GetComponent<Image>().color = CurrentlyUnselected;
            selected = false;
            this.GetComponent<Animator>().SetBool("IsSelected", false);
        }
        
    }
}
