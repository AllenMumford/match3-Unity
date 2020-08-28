using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class GameManagerScript : MonoBehaviour
{

    public GameObject board; //Panel that acts as background board
    public GameObject[] tokenTypes; //All types of tokens (Red, Blue, Green, Brown, etc.) in the game. Each should have their own tag.
    public int boardWidth; //number of cells in row
    public int boardHeight; //number of cells in column
    public GameObject[] tokens; //Board. Is effectively a 2D array, starting from bottom left and wrapping upwards at end of line.
    public RectTransform boardRectTransform;
    public Canvas canvas;
    public int selected; //Will have index of selected token if selected, otherwise will have -1 to show nothing is selected
    public float tokenSpeed;
    public int score;
    public Text scoreTextBox;
    public float TimeDestructionAnimationLasts;
    public GameObject scoreboard;
    public GameObject TokenBlock;
    public Text MoveCounterTextBox;
    public GameObject MoveCounterObject;
    public string playerName;
    public GameObject endGameButton;


    private int cellWidth; //pixel width of a cell
    private int cellHeight; //pixel height of a cell
    private float boardX; //far left edge of board
    private float boardY; //far lower edge of board
    public enum Direction { NORTH, EAST, SOUTH, WEST };
    private List<Transform> movingTokensList; //maintains list of all tokens that are currently moving. created in Start()
    private List<Vector3> movingTokensDestinationList; //maintains list of movingTokens's destinations. The destination in this list is the destination for the token.transform in the movingTokens list OF THE SAME INDEX. Created in Start()
    private List<int> movedSinceLastProcessing; //A list of all indexes of tokens that are currently moving or have moved and haven't been checked for matches.
    private bool playerMoved; //set to true if the last thing that happened was a player move/swap. Used to differentiate player moves and cascading matches.
    //private bool attemptingMove; //set to true when a move/swap is attempted, but before check is made to see if the move is allowed. Set to false immediately after move succeeds or fails.
    private bool playerMoveFailed; //starts false. Is set to true when a player attempts a move and that move is not allowed. Set back to false once that move is undone.
    //private bool IsTokensBeingDestroyed; 
    private List<int> tokensBeingDestroyed; //list of all tokens currently in the destruction animation
    private float timeDestructionEnds; //Time when all tokens will be finished with their destruction animation
    private int playerMoveCount; //number of moves made by the player. Incremented when player makes a move that is accepted, before match is processed.
    private string tokenBlockTag; //tag taken from TokenBlock gameobject.

    /*  Wording guide:
        "Location" or "loc": type Vector2Int, location on board/grid of token in question. loc.x should be between 0 and (boardWidth-1). loc.y should be between 0 and (boardHeight-1).
        "Position": type Vector3, position is the Vector of the transform component of the object in question.
        "index": usually refers to the index of a token in the "tokens" array
        
        */

    /* GAME GOALS
     * NEXT UP:
     * NEEDS:
     * 
     * End Screens
     * Limiting factor (turns, time, no moves, no moves w/ blocks being added, etc.)
     * 
     * 
     * ACTUAL GAME:
     * Player Powers
     * Special tokens
     * Hints?
     * 
     * 
     * Destroy token animation speed still has to be manually edited
     * 
     * 
     * DONE:
     * Cascading matches
     * make getMatch check if given index is destroyed and just return empty match? In case first match found includes second match?
     * Score system (square of tokens matched?)
     * limit movements by adjacent and only if a match is produced
     * Populate board at beginning removes matches (gets all matches, replaces them with random tokens)
     * swap actually uses moveSpeed
     * Destroy token animation
     * Start Screen
     * 
     */

    //sets score to scoreNew. Updates UI in game. Updates PlayerPrefs score
    public void updateScore(int scoreNew)
    {
        scoreTextBox.text = "" + scoreNew;
        PlayerPrefs.SetInt("CurrentPlayerScore", scoreNew);
    }

    public void updateMoveCount(int moveCount)
    {
        MoveCounterTextBox.text = "" + moveCount;
        PlayerPrefs.SetInt("CurrentPlayerMoves", moveCount);
    }

    //checks moving tokens list and tokens being destroyed list. Returns true if either has at least one member. Otherwise returns false.
    public bool IsCurrentlyAnimating()
    {
        if (movingTokensList.Count > 0 || tokensBeingDestroyed.Count > 0)
        {
            return true;
        } else
        {
            return false;
        }
    }

    //Fill board with tokens. It then checks for moves, then deletes and replaces each token in the match with another randomly generated token. It repeats this 100 times, which should ensure everything is caught. It's increidbly unlikely to even repeat this 4 or 5 times. This might cause issues if 4 or less token types are used.
    void PopulateBoard()
    {
        for (int i = 0; i < tokens.Length; i++)
        {
            SpawnRandomTokenAtIndex(i);
        }

        List<List<int>> matches = getMatches();
        int SAFECOUNT = 0;

        while (matches.Count > 0 && SAFECOUNT < 100)
        {
            
            foreach (List<int> match in matches)
            {
                //Debug.Log("Populating produced " + tokens[match[0]].tag + "match at index " + match[0]);
                foreach (int ind in match)
                {
                    Destroy(tokens[ind]);
                    tokens[ind] = null;
                    SpawnRandomTokenAtIndex(ind);
                }
                
            }

            matches = getMatches();

            //if (SAFECOUNT == 99) Debug.Log("Looped 100 times, then quit. Something is probably wrong.");
            SAFECOUNT++;
        }


    }

    //calculates location of a token that would be at parameter "index" and calls InstantiateRandomTokenAtLocation on that location. Puts returned token into "tokens" array
    public void SpawnRandomTokenAtIndex(int index)
    {
        tokens[index] = InstantiateRandomTokenAtLocation(ConvertIndexToLocation(index));
    }

    //creates a Token at location parameter
    public GameObject InstantiateRandomTokenAtLocation(Vector2Int loc)
    {
        int type = Random.Range(0, tokenTypes.Length);
        GameObject token = Instantiate(tokenTypes[type], ConvertLocationToTransformVector3(loc), Quaternion.identity, canvas.transform);
        TokenSelectedScript tokenScript = token.GetComponent<TokenSelectedScript>();
        tokenScript.gameManager = this.gameObject;
        tokenScript.xOnBoard = loc.x;
        tokenScript.yOnBoard = loc.y;

        return token;
    }

    //Converts a given {x,y} location on board to int value in index
    int ConvertLocationToIndex(Vector2Int loc)
    {
        return loc.x + (loc.y * boardWidth);
    }

    Vector2Int ConvertIndexToLocation(int ind)
    {
        return new Vector2Int(ind % boardWidth, ind / boardWidth);
    }

    //Vector3 is the transform.position of the an object at cell at loc. This can give positions outside of board.
    Vector3 ConvertLocationToTransformVector3(Vector2Int loc)
    {
        return new Vector3((loc.x * cellWidth) + boardX + (cellWidth / 2), (loc.y * cellHeight) + boardY + (cellHeight / 2), 0);
    }

    //starting calculations for pixel positions
    void CalculatePixels()
    {
        float boardPixelWidth = boardRectTransform.rect.width;
        float boardXInCanvas = boardRectTransform.rect.x;
        float boardPixelHeight = boardRectTransform.rect.height;
        float boardYInCanvas = boardRectTransform.rect.y;

        cellWidth = (int)boardPixelWidth / boardWidth;
        cellHeight = (int)boardPixelHeight / boardHeight;

        boardX = (int)(boardXInCanvas + canvas.transform.position.x);
        boardY = (int)(boardYInCanvas + canvas.transform.position.y);
    }

    //Accept message from Tokens, then run the relevant code
    public void InformTokenSelected(GameObject token, int x, int y)
    {
        //Checks to see if tokens are currently being animated, if so ignore selection
        if (IsCurrentlyAnimating())
        {
            return;
        }

        
        Vector2Int loc = new Vector2Int(x, y);
        int index = ConvertLocationToIndex(loc);
        if (selected == -1)
        {
            //Nothing was selected, this token becomes selected
            
            if (!(token.CompareTag(tokenBlockTag))) //the selected token is not a block, so it can be normally selected.
            {
                token.GetComponent<TokenSelectedScript>().setSelected(true);
                selected = index;
            }

        } else if (selected == index)
        {
            //same token was clicked again, deselect that token
            deselect();
        } else
        {
            //different token clicked, a move is attempted, then deselect

            //only do anything if the move is legal
            if (isAdjacent(selected, index))
            {

                //GameObject prevSelectedToken = tokens[selected];

                //BASIC MOVE
                swap(ConvertIndexToLocation(deselect()), loc);
                //set flags
                playerMoved = true;
                //attemptingMove = true;

                //POINT WHERE THIS CODE SHOUDL END

                /*
                //check for matches on both ends of swap, process matches if they are larger than 0
                List<int> match1 = getMatch(selected);
                List<int> match2 = getMatch(index);

                if (match1.Count > 0 || match2.Count > 0) //if there was a match produced, process
                {
                    if (match1.Count > 0) processMatch(match1);
                    if (match2.Count > 0) processMatch(match2);
                } else //if no match was produced, swap them back
                {
                    swap(ConvertIndexToLocation(selected), loc);
                }
                //set tokens to deselected internally
                //token.GetComponent<TokenSelectedScript>().setSelected(false); //currently unnecessary
                prevSelectedToken.GetComponent<TokenSelectedScript>().setSelected(false);

                //set no token selected in GM
                selected = -1;

                RepopulateBoard();
                */

            } else //if move wasn't legal, deselect the selected token to reset.
            {
                deselect();
            }
        }

    }

    //called if tokens just stopped moving and the player swap was the last thing that happened.
    private void processPlayerMoveSwap()
    {
        if (playerMoveFailed)
        {
            //got to this point because a player swap was attempted, failed, undone, and this was called as soon as no tokens were moving.
            playerMoveFailed = false;
            playerMoved = false;
            movedSinceLastProcessing.Clear();
        }
        else
        {
            //got to this point because a player attempted a swap

            

            //find indexes of the two swapped tokens
            int index1 = movedSinceLastProcessing[0];
            int index2 = movedSinceLastProcessing[1];

            //check for matches on both ends of swap, process matches if they are larger than 0. 
            List<int> match1 = getMatch(index1);
            List<int> match2 = getMatch(index2);

            //matches are checked, set attempting move to false
            //attemptingMove = false;

            if (match1.Count > 0 || match2.Count > 0) //if there was a match produced, process
            {
                playerMoveCount++;
                updateMoveCount(playerMoveCount);
                if (match1.Count > 0) processMatch(match1);
                if (match2.Count > 0) processMatch(match2);
                movedSinceLastProcessing.Clear();
                playerMoved = false;
            }
            else //if no match was produced, swap them back
            {
                playerMoveFailed = true;
                movedSinceLastProcessing.Clear();
                swap(index1, index2);
            }
            //set tokens to deselected internally
            //token.GetComponent<TokenSelectedScript>().setSelected(false); //currently unnecessary
            //prevSelectedToken.GetComponent<TokenSelectedScript>().setSelected(false);
            deselectToken(index1);
            deselectToken(index2);

            //set no token selected in GM
            selected = -1;

            //RepopulateBoard(); //changed to do this after update that finishes destruction animation
        }
    }

    //called immediately when the board settles. (When pieces have fallen after a successful move, and no more cascades are happening.) Currently used to implement Blocking.
    private void boardSettled()
    {
        if (playerMoveCount % 5 == 0) //Only do this every 5 moves.
        {
            //Debug.Log("board Settle % 5 trigger");
            spawnBlockAtLowestIndex();
        }
    }

    //checks through tokens[] in order, checking tags. First token found whose tag is not matching the Token Block tag is replaced with a Token Block. Returns the index that was replaced with a Block.
    private int spawnBlockAtLowestIndex()
    {
        int targetIndex = -1;
        //currently not random. Should make random? Random space on the lowest row gets a block?
        for (int i = 0; i < tokens.Length; i++)
        {
            GameObject token = tokens[i];
            if ( ! (token.CompareTag(tokenBlockTag))) //if the token isn't a block, store its index and break the loop.
            {
                targetIndex = i;
                break;
            }
        }

        Vector2Int loc = ConvertIndexToLocation(targetIndex);
        GameObject TokenBlockInstance = Instantiate(TokenBlock, ConvertLocationToTransformVector3(loc), Quaternion.identity, canvas.transform);
        TokenSelectedScript tokenScript = TokenBlockInstance.GetComponent<TokenSelectedScript>();
        tokenScript.gameManager = this.gameObject;
        tokenScript.xOnBoard = loc.x;
        tokenScript.yOnBoard = loc.y;
        Destroy(tokens[targetIndex]);
        tokens[targetIndex] = TokenBlockInstance;

        return targetIndex;
    }

    //deselects whatever is currently selected by GM in "selected". Tells token to deselect self. Returns the int that was in "selected"
    public int deselect()
    {
        if (selected == -1) return -1;
        if (tokens[selected] != null) tokens[selected].GetComponent<TokenSelectedScript>().setSelected(false);
        int ret = selected;
        selected = -1;
        return ret;
    }

    //tells token at "index" to deselect self. DOES NOT SET SELECTED TO -1
    public void deselectToken(int index)
    {
        if (tokens[index] == null) return;
        tokens[index].GetComponent<TokenSelectedScript>().setSelected(false);
    }

    /* isLegalMove() useless right now
    //checks whether it would be legal to swap the two indexes. DOES NOT CHECK IF A MATCH WOULD BE MADE
    public bool isLegalMove(int index1, int index2)
    {
        //only checks if the spaces are adjacent.
        return isAdjacent(index1, index2);
    }
    */

    //repopulates column by column. Moves tokens above empty spaces down. Spawns tokens above board and move down to proper place.
    public void RepopulateBoard()
    {
        
        for (int i = 0; i < boardWidth; i++)
        {
            RepopulateColumn(i);
        }

    }

    //repopulates, but only the indicated column. "col" should be the index of the lowest token in the desired column. "col" should be between 0 and (boardWidth - 1)
    public void RepopulateColumn(int col)
    {
        int countSpawned = 0;
        for (int i = col; i < tokens.Length; i += boardWidth)
        {
            if (tokens[i] == null)
            {
                int indexAbove = getClosestTokenAbove(i);

                if (indexAbove == -1)
                {
                    tokens[i] = InstantiateRandomTokenAtLocation(new Vector2Int(col, boardHeight + countSpawned));
                    countSpawned++;
                    moveTokenToIndex(tokens[i], i);
                }
                else
                {
                    moveTokenAtIndexToIndex(indexAbove, i);
                }
            }
        }
    }

    //returns index of nearest token directly North of index. Returns -1 if out of bounds is reached before a token is found.
    public int getClosestTokenAbove(int index)
    {
        int indexAbove = getAdjacentIndex(index, Direction.NORTH);
        while (indexAbove != -1 && tokens[indexAbove] == null)
        {
            indexAbove = getAdjacentIndex(indexAbove, Direction.NORTH);
        }

        if (indexAbove == -1)
        {
            return -1;
        } else
        {
            return indexAbove;
        }
    }

    //move token at indexOrigin to indexDestination, assume destination is empty
    public void moveTokenAtIndexToIndex(int indexOrigin, int indexDestination)
    {
        //change index of token
        tokens[indexDestination] = tokens[indexOrigin];

        //call moveTokenToIndex
        moveTokenToIndex(tokens[indexDestination], indexDestination);

        //set original place to null
        tokens[indexOrigin] = null;
    }

    //changes internal location and adds token to move List. DOES NOT ASSIGN TOKEN TO NEW INDEX IN "tokens"
    public void moveTokenToIndex(GameObject token, int indexDestination)
    {
        //change internal location
        TokenSelectedScript tokenScript = token.GetComponent<TokenSelectedScript>();
        Vector2Int loc = ConvertIndexToLocation(indexDestination);
        tokenScript.xOnBoard = loc.x;
        tokenScript.yOnBoard = loc.y;

        //update position
        //UpdateInternalPosition(indexDestination);
        //Instead: start token moving to destination
        movingTokensList.Add(token.transform);
        movingTokensDestinationList.Add(ConvertLocationToTransformVector3(ConvertIndexToLocation(indexDestination)));
        //Mark tokens as having moved since last check
        movedSinceLastProcessing.Add(indexDestination);

    }

    //process a match. Destory all tokens in match. Update Score based on match
    public void processMatch(List<int> match)
    {
        //score points equal to number of tokens in match, squared
        int pointsScored = match.Count * match.Count;
        score = score + pointsScored;
        updateScore(score);

        foreach (int index in match)
        {
            destToken(index);

            //Destroy(tokens[index]);
            //tokens[index] = null;
        }
    }

    //adds tokens[index] to destroying tokens list, starts destruction animation, DOES NOT "Destroy()" TOKEN OR SET ANYTHING TO NULL;
    public void destToken(int index)
    {
        tokensBeingDestroyed.Add(index);
        tokens[index].GetComponent<Animator>().SetBool("IsDestroyed", true);
        timeDestructionEnds = Time.time + TimeDestructionAnimationLasts;
    }

    /* getMatch old code
    //checks to see if there is a match at the location/index provided. A match is at least 3 tokens of the same color in a row. Returns a list of all indexes of tokens in the match. Returns an empty list if there is not match;
    public List<int> checkMatchIfSwap(int index1, int index2)
    {
        //DEBUG
        //Debug.Log("Checking for Match at: " + index);

        List<int> ret = new List<int>();

        //central token tag. Compare to other tags to see if match
        string mainTag = tokens[index].tag;

        //Go through each direction
        //North/South
        List<int> northSouth = new List<int>();
        northSouth = addUntilNotSame(index, mainTag, Direction.NORTH, northSouth);
        northSouth = addUntilNotSame(index, mainTag, Direction.SOUTH, northSouth);
        if (northSouth.Count >= 2)
        {
            ret.AddRange(northSouth);
        }

        //East/West
        List<int> eastWest = new List<int>();
        eastWest = addUntilNotSame(index, mainTag, Direction.EAST, eastWest);
        eastWest = addUntilNotSame(index, mainTag, Direction.WEST, eastWest);
        if (eastWest.Count >= 2)
        {
            ret.AddRange(eastWest);
        }

        //If a match was found NS or EW, add the original token to it.
        if(ret.Count >= 2)
        {
            ret.Add(index);
        }

        return ret;
    }

    */

    //get full match that index is a part of. First token in list is the origin of the call
    public List<int> getMatch(int index)
    {
        /* LOGIC: Run NS check for matches on first token. Any indexes added to list are marked NORTH. Maintain second list "directionFound" storing direction
         * int "countProcessed" = 0, this will keep track of what tokens in the "match" List you have processed already. number will be first unprocessed index
         * Run EW check for matches on all tokens in "match" list, marking those found as EAST
         * while countProcessed < match.length, process the next token
         *  next token should be checked EW if it was marked NORTH or SOUTH, or checked NS if it was marked EAST or WEST
         *  if at least 2 tokens are found (as normal) then for each check if it is already in the match, if not add it and mark its direction
         *  increment countProcessed
         * Resulting match list should be full match
        */

        //if the token at the index is null/gone/destroyed/Block, return an empty list
        if (tokens[index] == null || tokens[index].CompareTag(tokenBlockTag)) return new List<int>();

        //List of indexes in match
        List<int> ret = new List<int>();
        ret.Add(index);

        //count and processDirection
        int countProcessed = 1; //starts at 1 (second token) because we process the first token explicitly
        List<Direction> directionFound = new List<Direction>();
        directionFound.Add(Direction.NORTH); //must put something in for the first token even though it will be explicitly processed

        //central token tag. Compare to other tags to see if match
        string mainTag = tokens[index].tag;

        //First check through each direction
        //North/South
        List<int> northSouth = new List<int>();
        northSouth = addUntilNotSame(index, mainTag, Direction.NORTH, northSouth);
        northSouth = addUntilNotSame(index, mainTag, Direction.SOUTH, northSouth);
        if (northSouth.Count >= 2)
        {
            foreach (int ind in northSouth)
            {
                ret.Add(ind);
                directionFound.Add(Direction.NORTH);
            }
        }

        //East/West
        List<int> eastWest = new List<int>();
        eastWest = addUntilNotSame(index, mainTag, Direction.EAST, eastWest);
        eastWest = addUntilNotSame(index, mainTag, Direction.WEST, eastWest);
        if (eastWest.Count >= 2)
        {
            foreach (int ind in eastWest)
            {
                ret.Add(ind);
                directionFound.Add(Direction.EAST);
            }
        }
        
        while (countProcessed < ret.Count)
        {
            //check East/West
            int indexOfTokenToBeProcessed = ret[countProcessed];
            if (directionFound[countProcessed] == Direction.NORTH)
            {
                eastWest = new List<int>();
                eastWest = addUntilNotSame(indexOfTokenToBeProcessed, mainTag, Direction.EAST, eastWest);
                eastWest = addUntilNotSame(indexOfTokenToBeProcessed, mainTag, Direction.WEST, eastWest);
                if (eastWest.Count >= 2)
                {
                    foreach (int ind in eastWest)
                    {
                        if (!(ret.Contains(ind)))
                        {
                            ret.Add(ind);
                            directionFound.Add(Direction.EAST);
                        }
                    }
                }
            } else
            {
                //Direction found will be EAST, so check North/South
                northSouth = new List<int>();
                northSouth = addUntilNotSame(indexOfTokenToBeProcessed, mainTag, Direction.NORTH, northSouth);
                northSouth = addUntilNotSame(indexOfTokenToBeProcessed, mainTag, Direction.SOUTH, northSouth);
                if (northSouth.Count >= 2)
                {
                    foreach (int ind in northSouth)
                    {
                        if (!(ret.Contains(ind)))
                        {
                            ret.Add(ind);
                            directionFound.Add(Direction.NORTH);
                        }
                    }
                }
            }

            countProcessed++;
        }

        //check if a match was actually found. If so return the built list, otherwise return an empty list.
        if (ret.Count >= 3)
        {
            return ret;
        } else
        {
            return new List<int>();
        }
    }
    
    //returns all matches on board
    public List<List<int>> getMatches()
    {
        //make list of every third index, ensuring the first index on each row is first, then second, then third, then first...
        //OR make list of all tokens that moved. Must add some array "moved[]" variable at top or something

        List<int> indexList = new List<int>();

        int startCount = 0;
        for (int i = 0; i < boardHeight; i++)
        {
            for (int j = startCount; j < boardWidth; j += 3)
            {
                indexList.Add((i * boardWidth) + j);
            }
            startCount = (startCount + 1) % 3;
        }

        return getMatches(indexList);
    }

    //checks all indexes given for matches. Returns list of all matches originating on those indexes. Automatically removes duplicate matches.
    public List<List<int>> getMatches(List<int> indexes)
    {
        List<List<int>> matches = new List<List<int>>();

        foreach (int index in indexes)
        {
            List<int> match = getMatch(index);
            if (match.Count > 0)
            {
                bool duplicate = false;
                for (int i = 0; i < matches.Count; i++)
                {
                    if (matches[i].Contains(match[0]))
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (!(duplicate))
                {
                    matches.Add(match);
                }
            }
        }

        return matches;
    }
    
    //recursively checks the token in the direction specified to see if it matches mainTag. If it does, add it to List ret and repeat. Stops when it reaches out of bounds, null token, or token that doesn't match mainTag. Returns List of all indexes of matching tokens found.
    public List<int> addUntilNotSame(int index, string mainTag, Direction dir, List<int> ret)
    {
        //DEBUG
        //Debug.Log("Add until not same at: " + index + " and direction: " + dir);

        int nextIndex = getAdjacentIndex(index, dir);
        if (nextIndex == -1 || tokens[nextIndex] == null)
        {
            return ret;
        } else if ( ! (tokens[nextIndex].CompareTag(mainTag)) )
        {
            return ret;
        } else
        {
            ret.Add(nextIndex);
            return addUntilNotSame(nextIndex, mainTag, dir, ret);
        }
    }

    //returns an int[] of length 4, [0] will be the index to the north, [1] will be East, [2] will be South, [3] will be West. An entry will be -1 if out of bounds
    public int[] getAllAdjacentIndexes(int index)
    {
        int[] ret = new int[4];
        ret[0] = getAdjacentIndex(index, Direction.NORTH);
        ret[1] = getAdjacentIndex(index, Direction.EAST);
        ret[2] = getAdjacentIndex(index, Direction.SOUTH);
        ret[3] = getAdjacentIndex(index, Direction.WEST);

        return ret;

    }

    //returns the index of the token in the direction represented by the direction parameter. NORTH, EAST, SOUTH, WEST. Returns -1 if out of bounds. Returns -10 if error. 
    public int getAdjacentIndex(int index, Direction direction)
    {
        switch (direction)
        {
            case Direction.NORTH:
                //North
                if (index + boardWidth >= boardWidth * boardHeight)
                {
                    return -1;
                } else
                {
                    return index + boardWidth;
                }
            case Direction.EAST:
                //East
                if (index % boardWidth == boardWidth - 1)
                {
                    return -1;
                } else
                {
                    return index + 1;
                }
            case Direction.SOUTH:
                //South
                if (index - boardWidth < 0)
                {
                    return -1;
                } else
                {
                    return index - boardWidth;
                }
            case Direction.WEST:
                //West
                if (index % boardWidth == 0)
                {
                    return -1;
                } else
                {
                    return index - 1;
                }
            default:
                //ERROR
                Debug.Log("Error direction unexpected: " + direction);
                return -10;
                
        }

    }

    //returns true if two indexes are adjacent. Uses getAllAdjacentIndexes
    public bool isAdjacent(int index1, int index2)
    {
        //checks each adjacent index
        int[] adjacents = getAllAdjacentIndexes(index1);
        foreach (int ind in adjacents)
        {
            if (ind == index2) return true;
        }
        return false;
    }

    //converts indexes to locations and calls swap(firstLoc, secondLoc)
    public void swap(int index1, int index2)
    {
        swap(ConvertIndexToLocation(index1), ConvertIndexToLocation(index2));
    }

    //swaps tokens at firstLoc and secondLoc. Swaps locations, updates locations, and "tokens[]" assignments
    public void swap(Vector2Int firstLoc, Vector2Int secondLoc)
    {
        int firstIndex = ConvertLocationToIndex(firstLoc);
        int secondIndex = ConvertLocationToIndex(secondLoc);

        GameObject firstToken = tokens[firstIndex];
        GameObject secondToken = tokens[secondIndex];

        moveTokenToIndex(firstToken, secondIndex);
        moveTokenToIndex(secondToken, firstIndex);

        /* THIS PART REPLACED BY MOVE
        TokenSelectedScript firstScript = tokens[firstIndex].GetComponent<TokenSelectedScript>();
        TokenSelectedScript secondScript = tokens[secondIndex].GetComponent<TokenSelectedScript>();

        //Need to swap positions on GM board array AND swap internally stored variables.

        //Swap internal locations
        int xOnBoardTEMP = firstScript.xOnBoard;
        int yOnBoardTEMP = firstScript.yOnBoard;

        firstScript.xOnBoard = secondScript.xOnBoard;
        firstScript.yOnBoard = secondScript.yOnBoard;
        secondScript.xOnBoard = xOnBoardTEMP;
        secondScript.yOnBoard = yOnBoardTEMP;
        */


        //Swap pointers in GM board array
        tokens[firstIndex] = tokens[secondIndex];
        tokens[secondIndex] = firstToken;

        /* THIS PART REPLACED BY MOVE
        //Need to update actual position
        UpdateInternalPosition(firstIndex);
        UpdateInternalPosition(secondIndex);
        */
    }

    //calc index based on loc, then call UpdateDrawPosition(index, loc)
    public void UpdateInternalPosition(Vector2Int loc)
    {
        UpdateInternalPosition(ConvertLocationToIndex(loc), loc);
    }

    //calc loc based on index, then call UpdateDrawPosition(index, loc)
    public void UpdateInternalPosition(int index)
    {
        UpdateInternalPosition(index, ConvertIndexToLocation(index));
    }
    
    //should only be called by other UpdateDrawPosition methods, otherwise behaviour might be weird
    private void UpdateInternalPosition(int index, Vector2Int loc)
    {
        tokens[index].transform.position = ConvertLocationToTransformVector3(loc);
    }
    
    //Called by End Game button
    public void EndGame()
    {
        //Load Main Menu Scene
        SceneManager.LoadScene(0);
    }

    // Start is called before the first frame update
    void Start()
    {
        tokens = new GameObject[boardWidth * boardHeight];
        score = 0;
        movedSinceLastProcessing = new List<int>();
        playerMoved = false;
        //attemptingMove = false;
        playerMoveFailed = false;
        //IsTokensBeingDestroyed = false;
        tokensBeingDestroyed = new List<int>();
        updateScore(score);
        CalculatePixels();
        scoreboard.transform.position = new Vector3(boardX / 2, boardRectTransform.position.y + 100, 0);
        MoveCounterObject.transform.position = new Vector3(boardX / 2, boardRectTransform.position.y - 100, 0);
        movingTokensList = new List<Transform>();
        movingTokensDestinationList = new List<Vector3>();
        playerMoveCount = 0;
        tokenBlockTag = TokenBlock.tag;
        PlayerPrefs.SetString("CurrentPlayerName", playerName);
        PlayerPrefs.SetInt("CurrentPlayerScore", 0);
        PlayerPrefs.SetInt("CurrentPlayerMoves", 0);
        endGameButton.transform.position = new Vector3(canvas.transform.position.x + (boardRectTransform.rect.width / 2) + (boardX / 2), boardRectTransform.position.y - 100, 0);


        PopulateBoard();
        selected = -1;
    }

    // Update is called once per frame
    void Update()
    {
        //if moves were made, check if all moves are finished, if so check for cascades.
        if (UpdateTokenMovements())
        {
            
            if (movingTokensList.Count > 0 || tokensBeingDestroyed.Count > 0) //tokens are still moving or being destroyed
            {
                
            }  else //no tokens are moving or being destroyed
            {
                
                if (playerMoved)
                {
                    //process move that player just made.
                    //currently assuming that the only move that can be made is swap
                    processPlayerMoveSwap();
                    
                }
                else
                {

                    //process board to check for cascades
                    List<List<int>> matches = getMatches();

                    //If there are matches to process, do that.
                    if (matches.Count > 0)
                    {
                        foreach (List<int> match in matches)
                        {
                            processMatch(match);

                        }

                    } else //There are no matches to process, the board has settled.
                    {
                        boardSettled();
                    }

                    //empty the movedSinceLastProcessing list
                    movedSinceLastProcessing.Clear();

                    //RepopulateBoard(); //changed to happen in update after destruction of tokens finishes.
                }
            }
        } else if (tokensBeingDestroyed.Count > 0) //no tokens are moving, and tokens are still being destroyed
        {
            //if the destruction animation has played out
            if (Time.time > timeDestructionEnds)
            {
                //Destroy() each token slated for destruction
                foreach (int index in tokensBeingDestroyed)
                {
                    Destroy(tokens[index]);
                    tokens[index] = null;
                }

                //clear the list of tokens to be destroyed
                tokensBeingDestroyed.Clear();

                //Tokens are finally actually gone. Repopulate
                RepopulateBoard();
            }

        }
    }

    //updates the position of tokens using movingTokensList and destination list, and removes any tokens that reach their destination. Returns true if moves were made, returns false if no moves were found to be updated
    public bool UpdateTokenMovements()
    {
        //if nothing is going to move, return false immediately.
        if (movingTokensList.Count == 0)
        {
            return false;
        }

        //Run through list, marking any moves that finish on the "done" array.
        bool[] done = new bool[movingTokensList.Count];
        //initialize done array to false
        for (int i = 0; i < done.Length; i++)
        {
            done[i] = false;
        }
        //run through moves and move tokens.
        for (int i = 0; i < movingTokensList.Count; i++)
        {
            movingTokensList[i].position = Vector3.MoveTowards(movingTokensList[i].position, movingTokensDestinationList[i], tokenSpeed * Time.deltaTime);
            if (movingTokensList[i].position == movingTokensDestinationList[i])
            {
                done[i] = true;
            }
        }
        //run through list backwards and remove all marked done
        for (int i = movingTokensList.Count - 1; i >= 0; i--)
        {
            if (done[i])
            {
                movingTokensList.RemoveAt(i);
                movingTokensDestinationList.RemoveAt(i);
            }
        }

        return true;
    }
}
