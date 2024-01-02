using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Schema;

namespace WinFormsChess
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            for ( int i = 0; i < 8; i++ )
            {
                for( int j = 0; j < 8; j++ )
                {
                    PictureBox node = (PictureBox)gridTLP.GetControlFromPosition( j,i );
                    int startRow = gridTLP.GetRow(node);                        //get the relevant information about the picturebox and store it as a pictureBoxInformation instance
                    int startCol = gridTLP.GetColumn(node);                     //...this allows for easier referencing in later steps
                    string pieceTitle = (string)node.Tag;
                    string pieceColor = pieceTitle.Substring(0, 1);
                    string pieceType = pieceTitle.Substring(1, pieceTitle.Length - 1);
                    board[i, j] = new pictureBoxInformation(startCol,startRow,pieceType,pieceColor);
                    Console.Write(pieceTitle + "\t");
                }
                Console.WriteLine();
            }
        }
        List<possible_move> Possible_moves = new List<possible_move>();
        bool whitePlayerTurn = true;                                //track who needs to move
        bool isFirstClick = true;                                   //differentiate between selecting a piece to move and selecting a destination square    
        bool foundCheck = false;                                    //denote that the active player is in check
        int candidateMoves = 0;                                     //track the number of moves the active player could make (ignoring those that put himself into check)
        int movesThatCauseCheck = 0;                                //if (candidateMoves-movesThatCauseCheck)==0, the game is in CheckMate 
        int whiteKingRow = 7, whiteKingCol = 4;                     //track the position of the white king                   
        int blackKingRow = 0, blackKingCol = 4;                     //track the position of the black king   
        int moveCount = 0;                                          //to display the total number of moves made so far
        int timeInSeconds = 0;                                      //display the time taken so far
        int[] knightRowVector = { -2, -2, -1, -1, 1, 1, 2, 2 };     //iterating through these two arrays in step gives the column and row offsets in the 8 possible knight moves 
        int[] knightColumnVector = { -1, 1, -2, 2, -2, 2, -1, 1 };  //^

        System.Drawing.Color lightSquareColor = System.Drawing.Color.LightYellow;   //the first 2 variables control the background color of the board squares
        System.Drawing.Color darkSquareColor = System.Drawing.Color.SandyBrown;     //^
        System.Drawing.Color activePieceColor=System.Drawing.Color.Red;             //color to highlight the active piece
        System.Drawing.Color checkPieceColor = System.Drawing.Color.Purple;         //color to highlight the pieces causing check (the king and an opponent piece)
        System.Drawing.Color promotionPieceColor = System.Drawing.Color.Green;      //color to highlight a pawn that has reached the opposite side and can be promoted
        System.Drawing.Color reachableSquareColor = System.Drawing.Color.Pink;      //color to highlight all of the squares the active piece can reach

        PictureBox firstSelection = null;                           //a handle to store the picturebox a player clicked on first (the piece they want to move)
        PictureBox secondSelection = null;                          //a handle to store the picturebox a player clicked on second (the destination square)
        PictureBox copyOfFirstSelection = new PictureBox();         //a blank picturebox that will clone and store the details (background Image & Tag) of the first seleted picturebox
        PictureBox copyOfSecondSelection = new PictureBox();        //^ but for the second selected picturebox
        PictureBox pieceCausingCheck = null;                        //allows us to highlight a piece that prevents a move being made

        static Random rnd = new Random();
        private pictureBoxInformation[,] board = new pictureBoxInformation[8, 8];
        

        private void onClick(object sender, EventArgs e)
        {
            Possible_moves.Clear();
            if (isFirstClick)       //player has clicked on a piece to select it for movement
            {
                firstSelection = sender as PictureBox;                                              //grab the clicked picturebox
                string pieceTag = (string)firstSelection.Tag;
                Console.WriteLine("first selection tag was " + firstSelection.Tag);
                if (whitePlayerTurn && pieceTag[0] == 'w' || !whitePlayerTurn && pieceTag[0] == 'b')    //if they chose one of their own pieces
                {
                    /*
                    TODO: chọn lượt chơi
                    đoạn này sẽ coi xem các gt của người chơi là gì 
                    player == whitePlayerTurn
                    computer == !whitePlayerTurn
                     */
                    cloneFirstSelectionPictureBox();                                                //store the attributes of the first picturebox before changing it
                    firstSelection.BackColor = activePieceColor;                            //highlight the cell                                                               
                    //TODO: dòng này sẽ cho phép coi các đoạn đường có thể xảy ra, 
                    scanForAvailableMoves(firstSelection);                                          //go though and find squares the piece can move to (and highlight them)
                    //TODO: Important (scanForAvailableMoves -> findReachableSquares)
                    candidateMoves = 0;                                                             //reset this for future use
                    movesThatCauseCheck = 0;                                                        //^
                    //TODO: dòng này sẽ lưu các TH xảy ra (bao gồm checkmate và chiếu)
                    isFirstClick = false;                                                           //note that we are moving on to the second click next time
                }
            }
            else                                                                                    //player has chosen a destination square to try to move a piece to
            {
                //SeconSelection: lựa chọn vị trí để đi
                secondSelection = sender as PictureBox;                                             //grab the clicked picturebox
                isFirstClick = true;
                //TODO: màu quay lại ban đầu nếu chọn những ô ko reachable 
                if (secondSelection.BackColor != reachableSquareColor)                         //if player selects any unhighlighted (non reachable) square
                {
                    unHighlightMoves();                                                             //remove the highlighting of all cells 
                }
                else                                                                                //player selected a highlighted square they may be able to move to
                {
                    if ((string)firstSelection.Tag == "wKing")
                    {
                        whiteKingCol = gridTLP.GetColumn(secondSelection);                          //store the king location if this is the piece they have moved
                        whiteKingRow = gridTLP.GetRow(secondSelection);
                    }
                    if ((string)firstSelection.Tag == "bKing")
                    {
                        blackKingCol = gridTLP.GetColumn(secondSelection);
                        blackKingRow = gridTLP.GetRow(secondSelection);
                    }
                    //TODO: ở dòng trên đều là store vị trí king cả 2
                    cloneSecondSelectionPictureBox();                                   //store its attributes in copyOfSecondSelection
                    updatePictureBoxesAfterMove();                                      //unhighlight the selected piece and update picturebox attributes to reflect the move
                    //TODO: dòng này lưu và cập nhật lại đường đi 
                    movesThatCauseCheck = 0;                                            //reset this value to zero before testForCheck()
                    testForCheck();                                                    //this function increments movesThatCauseCheck by one if the current move will put the moving player in check
                    //nước đi đó có gây ra check hay không
                    if (movesThatCauseCheck == 1)                                       //player put themself in check -> need to undo move
                    {
                        unHighlightMoves();
                        undoMove();                                                     //revert pictureboxes, restore the king position if it tried to move, unhighlight cells
                    }
                    else                                                                //successful move
                    {
                        //đổi lượt
                        whitePlayerTurn = !whitePlayerTurn;                             //switch turn
                        movesThatCauseCheck = 0;                                        //reset before selectAllPieces()
                        candidateMoves = 0;                                             //^
                        //Xét các nước đối thủ có thể đi
                        //TODO: ví dụ trắng đi nước thì ở đấy sẽ xét đen
                        selectAllPieces();                                              //goes through all of the possible moves of the active player (not the one who just moved)  
                                                                                        //...and finds the total number of candidate moves and the number that put themself in check
                        unHighlightMoves();
                        if (movesThatCauseCheck == candidateMoves)                      //the player who just moved has left his opponent no viable moves -> CheckMate       
                        {
                            //TODO: checkmate
                            MessageBox.Show("CheckMate!");
                            checkMateSequence();
                        }
                    }
                    updateDisplay();                                                    //reflect whose turn it is and the number of moves made
                //TODO: dòng này chỉ để update vị trí đã đi và trả về
                }
            }
/*            Console.WriteLine("\nBEFORE PROMOTION");
            foreach (possible_move item in Possible_moves)
            {
                Console.WriteLine("{0} in box {1}, {2} moves to box {3}, {4}", item.current_piece.pieceColor + item.current_piece.pieceType,
                    item.current_piece.startCol, item.current_piece.startRow, item.next_col, item.next_row);
            }*/
            Control endRowControl = checkEndRows();                                     //if a pawn has reached the opposite end of the board this returns that control, otherwise null
            if (endRowControl != null)
            {
                Console.WriteLine("\nPROMOTION");
                //TODO: pawn tới đích sẽ chạy dòng này (phong tướng)
                whitePlayerTurn = !whitePlayerTurn;     //temporarily switch back to whitePlayerTurn to make switchToPromotionMenu() more inutitive. It is switched back in the function
                switchtoPromotionMenu();   //pops up a menu where the played can choose a piece to promote their pawn into, updates the pieces and tests if the promotion causes checkmate
            }


            //*********Bot Turn*********
            //Console.WriteLine("\nAFTER PROMOTION");
            /*foreach (possible_move item in Possible_moves)
            {
                Console.WriteLine("{0} in box {1}, {2} moves to box {3}, {4}", item.current_piece.pieceColor + item.current_piece.pieceType,
                    item.current_piece.startCol, item.current_piece.startRow, item.next_col, item.next_row);
            }*/

            //Bot turn
            /*bool BotTurn = !whitePlayerTurn ? true : false;
            Console.WriteLine("Is bot(black) turn: " + BotTurn);
            if (BotTurn && endRowControl == null)
            {
                //Task.Delay(500).Wait();
                BotPlay();
                IsInMinimax = false;
            }*/
        }

        public void BotPlay()
        {
            MarkingBotTurn();
            whitePlayerTurn = !whitePlayerTurn;
            updateDisplay();
            Control endRowControl = checkEndRows();
            if (endRowControl != null)
            {
                whitePlayerTurn = !whitePlayerTurn;
                switchtoPromotionMenu();
            }
        }

        public void MarkingBotTurn()
        {
            int r = rnd.Next(Possible_moves.Count);
            List<possible_move> tempMovesList = new List<possible_move>(Possible_moves);
            int res = MiniMax(board,false, tempMovesList, DefaultBeyondSteps, int.MinValue, int.MaxValue);
            //possible_move test_move = findingMove;
            //Console.WriteLine("{0} moves to [{1},{2}]",test_move.current_piece.pieceColor + test_move.current_piece.pieceType,test_move.next_row,test_move.next_col);
            //tempMovesList.RemoveAt(0);
            //Console.WriteLine("Actual list: {0}; Temp list: {1}",Possible_moves.Count, tempMovesList.Count);
            //int move = Minimax();
            possible_move next_move = findingMove;
            //
            firstSelection = (PictureBox)gridTLP.GetControlFromPosition(next_move.current_piece.startCol, next_move.current_piece.startRow);
            secondSelection = (PictureBox)gridTLP.GetControlFromPosition(next_move.next_col, next_move.next_row);
            if ((string)firstSelection.Tag == "bKing")
            {
                blackKingCol = gridTLP.GetColumn(secondSelection);
                blackKingRow = gridTLP.GetRow(secondSelection);
            }
            cloneFirstSelectionPictureBox();
            cloneSecondSelectionPictureBox();
            updatePictureBoxesAfterMove();
        }

        public void cloneFirstSelectionPictureBox()
        {
            copyOfFirstSelection.BackColor = firstSelection.BackColor;     //store the attributes of the first picturebox before changing it                 
            copyOfFirstSelection.Image = firstSelection.Image;
            copyOfFirstSelection.Tag = firstSelection.Tag;
        }
        public void cloneSecondSelectionPictureBox()
        {
            copyOfSecondSelection.Tag = secondSelection.Tag;               //store the attributes of the second picturebox before changing it
            copyOfSecondSelection.Image = secondSelection.Image;
            copyOfSecondSelection.BackColor = secondSelection.BackColor;
        }
        public void updatePictureBoxesAfterMove()
        {

            int beforeMove_column = gridTLP.GetColumn(firstSelection);
            int beforeMove_row = gridTLP.GetRow(firstSelection);
            string beforeMove_type = board[beforeMove_row, beforeMove_column].pieceType;
            string beforeMove_color = board[beforeMove_row, beforeMove_column].pieceColor;


            int afterMove_column = gridTLP.GetColumn(secondSelection);
            int afterMove_row = gridTLP.GetRow(secondSelection);

            board[afterMove_row,afterMove_column].pieceType = beforeMove_type;
            board[afterMove_row,afterMove_column].pieceColor = beforeMove_color;
            board[afterMove_row, afterMove_column].startCol = afterMove_column;
            board[afterMove_row, afterMove_column].startRow = afterMove_row;

            board[beforeMove_row,beforeMove_column].startCol = beforeMove_column;
            board[beforeMove_row,beforeMove_column].startRow = beforeMove_row;
            board[beforeMove_row,beforeMove_column].pieceColor = "e";
            board[beforeMove_row,beforeMove_column].pieceType = "mpty";


            firstSelection.BackColor = copyOfFirstSelection.BackColor;          //revert first picturebox to original (unhighlighted) colour
            secondSelection.Tag = firstSelection.Tag;                           //give the first picturebox the attributes of the square it is moving to
            secondSelection.Image = firstSelection.Image;                       //^
            firstSelection.Tag = "empty";                                       //change the attributes of the original square to reflect that there is no longer a piece there
            firstSelection.Image = null;                                        //^

            printBoard(board);

        }
        public void undoMove()
        {
            secondSelection.BackColor = activePieceColor;                                                       //highlight the current piece
            pieceCausingCheck.BackColor= checkPieceColor;                                                   //highlight the piece causing check
            if (whitePlayerTurn)
            {
                gridTLP.GetControlFromPosition(whiteKingCol, whiteKingRow).BackColor= checkPieceColor;      //highlight the king that is in check
            }
            else
            {
                gridTLP.GetControlFromPosition(blackKingCol, blackKingRow).BackColor = checkPieceColor;
            }

            MessageBox.Show("Invalid Move: Your king is in check");

            firstSelection.Tag = copyOfFirstSelection.Tag;                  //return the selected pictureboxes back to their original status
            firstSelection.Image = copyOfFirstSelection.Image;
            secondSelection.Tag = copyOfSecondSelection.Tag;
            secondSelection.Image = copyOfSecondSelection.Image;

            if (whitePlayerTurn && (string)firstSelection.Tag == "wKing")     //if a player moved their king, update the ints that store where it is located to reflect undoing the move
            {
                whiteKingCol = gridTLP.GetColumn(firstSelection);
                whiteKingRow = gridTLP.GetRow(firstSelection);
            }
            if (!whitePlayerTurn && (string)firstSelection.Tag == "bKing")    //^
            {
                blackKingCol = gridTLP.GetColumn(firstSelection);
                blackKingRow = gridTLP.GetRow(firstSelection);
            }
            unHighlightMoves();                                             //restore colours back to normal
        }
        private void updateDisplay()
        {   //called after each turn
            moveCount++;
            movesLabel.Text = "Move number: " + moveCount;
            if (!whitePlayerTurn)
            {
                playerTurnLabel.Text = "Black player: it's your turn.";
            }
            else
            {
                playerTurnLabel.Text = "White player: it's your turn.";
            }
        }
        public void checkMateSequence()
        {
            gridTLP.Enabled = false;                    //disable the normal grid
            playerTurnLabel.Visible = false;            //make the other display items that are in the way of the pomotion information invisible
            movesLabel.Visible = false;                 //^
            gameTimeLabel.Visible = false;              //^
            timer.Stop();                               //halt the timer to display the time the game took
            if (whitePlayerTurn) {                        
                checkMateLabel.Text = "Black player is the winner! Play again?";            //white is in check
            }
            else
            {
                checkMateLabel.Text = "White player is the winner! Play again?";            //black is in check    
            }
            checkMateTLP.Visible = true;


        }
        public class pictureBoxInformation
        {       //this class is not functionally neccesary, but provides easier access to the properties of the PictureBox it derives from
                //that way we can call on (for example) 'activePicBoxInfo.pieceColor' (rather than calling on '((string)activePicBox.Tag).Substring(0, 1)'
            public int startRow;        //the row the control was located in
            public int startCol;        //the column the control was located in
            public string pieceType;    //"eg. 'Pawn' or 'Rook'
            public string pieceColor;   //either 'b' or 'w'
            public pictureBoxInformation(int _startCol, int _startRow, string _pieceType, string _pieceColor) 
            {
                startCol = _startCol;
                startRow = _startRow;
                pieceType = _pieceType;
                pieceColor = _pieceColor;
            }

            public pictureBoxInformation(pictureBoxInformation new_node)
            {
                startCol = new_node.startCol;
                startRow = new_node.startRow;
                pieceType = new_node.pieceType;
                pieceColor = new_node.pieceColor;
            }

        };
        private void selectAllPieces()
        {       //selects all of the players pieces and test how many moves they can make in total
            //TODO: Importain
            /*
             Dòng này sẽ hiện trong output, nó sẽ check hệt toàn bộ những khả năng xảy ra (make, cause check, possible move)
             */
            List<Control> playersRemainingPieces = new List<Control>();     //a list to store all of the controls that represent the current players pieces 
            foreach (Control c in gridTLP.Controls)
            {
                if (c is PictureBox)
                {
                    string thisPiece = (string)c.Tag;
                    if (thisPiece.Substring(0, 1) == "w")
                    {
                        if (whitePlayerTurn)                                  //only add it to the list if it begins with the correct letter (w for white, b for black)
                        {
                            playersRemainingPieces.Add((Control)c);
                        }
                    }
                    else if (thisPiece.Substring(0, 1) == "b")
                    {
                        if (!whitePlayerTurn)
                        {
                            playersRemainingPieces.Add((Control)c);
                        }
                    }
                }
            }
            candidateMoves = 0;                                             //reset these as they are updated in scanForAvailableMoves()
            movesThatCauseCheck = 0;                                        //^

            for (int i = 0; i < playersRemainingPieces.Count; i++)          //go thorough all of the players pieces in turn
            {
                scanForAvailableMoves(playersRemainingPieces[i]);           //count the number of candidateMoves and the number that put themself in check 
                //Console.WriteLine(playersRemainingPieces[i].Tag);
                //TODO: Add vào list moves 
            }

            if (whitePlayerTurn)                                              //console output - this is only used to demonstrate and debug the program                                         
            {
                Console.WriteLine("white can make " + candidateMoves + ". Of these, " + movesThatCauseCheck + " cause check, leaving " + (candidateMoves - movesThatCauseCheck) + " possible moves");
            }
            else
            {
                Console.WriteLine("black can make " + candidateMoves + ". Of these, " + movesThatCauseCheck + " cause check, leaving " + (candidateMoves - movesThatCauseCheck) + " possible moves");
            }
        }
        private void scanForAvailableMoves(Control activePicBox)
        {
            //this function is called for 2 reasons:
            // 1) to highlight the available moves for a piece a played has selected
            // 2) to count the number of moves a piece can make, and the number that cause check 

            int startRow = gridTLP.GetRow(activePicBox);                        //get the relevant information about the picturebox and store it as a pictureBoxInformation instance
            int startCol = gridTLP.GetColumn(activePicBox);                     //...this allows for easier referencing in later steps
            string pieceTitle = (string)activePicBox.Tag;
            string pieceColor = pieceTitle.Substring(0, 1);
            string pieceType = pieceTitle.Substring(1, pieceTitle.Length - 1);
            pictureBoxInformation currentPiece = new pictureBoxInformation(startCol, startRow, pieceType, pieceColor);
            //TODO: xác định cái Piece hiện tại từ col, row, tên, màu
            findReachableSquares(currentPiece);                                 //use the newly created pictureBoxInformation to find which squares are reachable
        }
        private void findReachableSquares(pictureBoxInformation currentMove)
        {
            switch (currentMove.pieceType)  //the possible moves depend on the piece type
            {
                case "Pawn":                                        //the most awkward type
                    if (currentMove.pieceColor == "w")              //movement dependent on color (unlike other pieces)
                    {                                               //first test for vertical only moves
                        if (canItMoveHere(0, -1, currentMove))      //canItMoveHere highlights only the cells we can move to based on the supplied movement vector arguments
                        {                                           //It returns true if the move is viable, otherwise false
                            
                            if (currentMove.startRow == 6)          //edge case where pawn is in start row and may be able to move 2 units
                            {
                                canItMoveHere(0, -2, currentMove);
                            }
                        }
                        canItMoveHere(-1, -1, currentMove); // đi chéo trái        //now test for diagonal attack moves
                        canItMoveHere(1, -1, currentMove);  // đi chéo phải        //^
                    }
                    else                                            //alternate situation where the tag of the active control begins with 'b' - it is a black piece
                    {
                        if (canItMoveHere(0, 1, currentMove))
                        {
                            if (currentMove.startRow == 1)
                            {
                                canItMoveHere(0, 2, currentMove);
                            }
                        }
                        canItMoveHere(-1, 1, currentMove);
                        canItMoveHere(1, 1, currentMove);
                    }
                    break;
                case "Knight":
                    for (int i = 0; i < 8; i++)                     //iterate through the global knightRowVector & knightColumnVector arrays to get the dy and dx values
                    {
                        canItMoveHere(knightRowVector[i], knightColumnVector[i], currentMove);
                    }
                    break;
                case "Rook":
                    testOrthogonal(currentMove);                    //a subfuction that handles vertical only and horizontal only moves
                    break;
                case "Bishop":
                    testDiagonal(currentMove);                      //a subfuction that handles moves that are both vertical and horizontal
                    break;
                case "Queen":
                    testOrthogonal(currentMove);
                    testDiagonal(currentMove);
                    break;
                case "King":
                    //Console.WriteLine("Test King Orthogonal");
                    testOrthogonal(currentMove);
                    //Console.WriteLine("Test King Diagonal");
                    testDiagonal(currentMove);
                    break;
            }
        }

        private void testOrthogonal(pictureBoxInformation currentMove)
        {
            int offset = 1;                                     //start off to the right of the active control and move right
            while (canItMoveHere(offset, 0, currentMove))       //call the function repeatedly until we find a non reachable square (or the edge of the board)
            {
                offset++;
            }
            offset = -1;                                        //start the left of the active control and move left
            while (canItMoveHere(offset, 0, currentMove))
            {
                offset--;
            }
            offset = 1;                                         //start below the active control and move down
            while (canItMoveHere(0, offset, currentMove))
            {
                offset++;
            }
            offset = -1;                                        //start above the active control and move up
            while (canItMoveHere(0, offset, currentMove))
            {
                offset--;
            }
        }
        private void testDiagonal(pictureBoxInformation currentMove)
        {
            int offset = 1;                                     //start below and to the right of the active control and move down and right
            while (canItMoveHere(offset, offset, currentMove))
            {
                offset++;
            }
            offset = 1;                                         //start above and to the right of the active control and move up and right
            while (canItMoveHere(offset, -offset, currentMove))
            {
                offset++;
            }
            offset = 1;                                         //start below and to the left of the active control and move down and left
            while (canItMoveHere(-offset, offset, currentMove))
            {
                offset++;
            }
            offset = 1;                                         //start above and to the left of the active control and move up and left
            while (canItMoveHere(-offset, -offset, currentMove))
            {
                offset++;
            }
        }
        
        public class possible_move
        {
            public pictureBoxInformation current_piece;
            public int next_col;
            public int next_row;
            
            public possible_move(pictureBoxInformation _current_piece, int _next_col, int _next_row)
            {
                current_piece = new pictureBoxInformation(_current_piece.startCol, _current_piece.startRow, _current_piece.pieceType, _current_piece.pieceColor);
                next_col = _next_col;
                next_row = _next_row;
            }
        }

        private bool canItMoveHere(int columnVector, int rowVector, pictureBoxInformation currentMove)
        {
            int col = currentMove.startCol + columnVector;      //the column containing the PictureBox we are testing
            int row = currentMove.startRow + rowVector;         //the row containing the PictureBox we are testing


            possible_move new_moves;
            //Console.WriteLine("Current col: {0}, row {1}", currentMove.startCol, currentMove.startRow);

            if (col >= 0 && col <= 7 && row >= 0 && row <= 7)                   //if we are still on the board (otherwise return false)
            {
                Control destination = gridTLP.GetControlFromPosition(col, row);                //grab the destination control
                string destinationTag = (string)destination.Tag;
                string destinationPieceColor = destinationTag.Substring(0, 1);                  //'b' for black, 'w' for white, 'e' for empty

                if (destinationPieceColor == "e")                                       //destination cell is empty
                {
                    //Console.WriteLine("Choose this option");
                    if (/*true*/!(/*false*/currentMove.pieceType == "Pawn" /*true*/ && columnVector != 0 /*false*/))        //check we are not in the edge case where pawn cannot move diagonal into empty space
                    {
                        if (!whitePlayerTurn && currentMove.pieceColor == "b") {
                            //Console.WriteLine("Only Black");
                            new_moves = new possible_move(currentMove,col, row);
                            Possible_moves.Add(new_moves);
                        }
                        
                        //Console.WriteLine("Condition is right");
                        //Nước đi là hợp lệ candidateMoves++
                        candidateMoves++;                                               //note that the move is viable (used when function if called to see if a player is in check)
                        if (IsInMinimax)//Đang sử dụng minimax
                        {
                            //Console.WriteLine("IsInMinimax");
                            Minimax_moveCanPlay++;
                            nextMoves.Add(BSIZE * row + col);
                        }

                        if (!IsInMinimax)
                        {
                            destination.BackColor = reachableSquareColor;              //highlight the cell (used when a player has chosen a piece to move)
                        }
                        Control startSquare = gridTLP.GetControlFromPosition(currentMove.startCol, currentMove.startRow);       //get a handle on the start square Control

                        string startSquareTitle = (string)startSquare.Tag;

                        if (startSquareTitle == "wKing") //Quân cờ được lựa chọn để đi là vua
                        {
                            whiteKingCol = col;                 //if the piece that was selected to move is a king, update its new position temporarily to the place it can reach
                            whiteKingRow = row;                 //..this allows us to test whether that move would put the player in check - it is put back afterwards
                        }
                        else if (startSquareTitle == "bKing")
                        {
                            blackKingCol = col;
                            blackKingRow = row;
                        }

                        string endSquareTitle = (string)destination.Tag;
                        destination.Tag = startSquare.Tag;              //temporarily update the tags as if the player has moved to test for check
                        startSquare.Tag = "empty";                      //...they will be reset after the testForCheck function

                        //TODO: Xét xem nước đi này có gây ra check ko
                        
                        int temp = movesThatCauseCheck; //số lượng move gây check trước khi xét
                        testForCheck();  //thay đổi movesThatCauseCheck          //test whether this move into an empty square will put self into check - if so, increment the movesThatCauseCheck function

                        //TODO: remove that move if it brings check
                        if (!whitePlayerTurn && currentMove.pieceColor == "b" && movesThatCauseCheck != temp) //nếu move này gây ra check --> xóa khỏi list move vừa add
                        {
                            Possible_moves.RemoveAt(Possible_moves.Count - 1);
                        }
                      
                        if (IsInMinimax && movesThatCauseCheck != temp)//Đang sử dụng minimax
                        {
                            Minimax_moveCauseCheck++;
                        }

                        //Reset nước đi được chọn lại hiện trạng ban đầu
                        startSquare.Tag = destination.Tag;              //the next few lines reset the board back to its state before the move          
                        destination.Tag = endSquareTitle;

                        if ((string)startSquare.Tag == "wKing")
                        {
                            whiteKingCol = currentMove.startCol;
                            whiteKingRow = currentMove.startRow;
                        }
                        else if ((string)startSquare.Tag == "bKing")
                        {
                            blackKingCol = currentMove.startCol;
                            blackKingRow = currentMove.startRow;
                        }


                        if (currentMove.pieceType == "King")
                        {
                            return false;           //edge case where King can only move one unit - return false to stop it trying to move further in the testOrthogonal & testDiagonal functions
                        }
                        return true;                //if the piece has the correct ability, we need to test if it can move further in this direction so return true
                    }
                }
                //Đụng mặt quân đối thủ
                else if (destinationPieceColor != currentMove.pieceColor)           //case where the active piece can reach an opponent piece
                {
                    //Console.WriteLine("Choose this option");
                    if (!(currentMove.pieceType == "Pawn" && columnVector == 0))    //exclude edge case where pawn cannot move forward to take a piece
                    {
                        if (!whitePlayerTurn && currentMove.pieceColor == "b")
                        {
                            //Console.WriteLine("Only Black 2");
                            new_moves = new possible_move(currentMove, col, row);
                            Possible_moves.Add(new_moves);
                        }
                        candidateMoves++;                                           //note that the move is possible (though it may cause check)
                        
                        if (IsInMinimax)//Đang sử dụng minimax
                        {
                            Minimax_moveCanPlay++;
                            nextMoves.Add(BSIZE * row + col);
                        }
                        if (!IsInMinimax)
                        {
                            destination.BackColor = reachableSquareColor;          //highlight it as a possible move
                        }
                        Control startSquare = gridTLP.GetControlFromPosition(currentMove.startCol, currentMove.startRow);   //get a handle on the start square Control
                        string startSquareTitle = (string)startSquare.Tag;
                        if (startSquareTitle == "wKing")
                        {
                            whiteKingCol = col;                 //if the piece that was selected to move is a king, update its new position temporarily to the place it can reach
                            whiteKingRow = row;                 //..this allows us to test whether that move would put the player in check - it is put back afterwards
                        }
                        else if (startSquareTitle == "bKing")
                        {
                            blackKingCol = col;
                            blackKingRow = row;
                        }
                        string endSquareTitle = (string)destination.Tag;
                        destination.Tag = startSquare.Tag;              //temporarily update the tags as if the player has moved to test for check
                        startSquare.Tag = "empty";                      //...they will be reset after the testForCheck function
                        int temp = movesThatCauseCheck;
                        testForCheck();                                 //test whether this attacking move will put self into check - if so, increment the movesThatCauseCheck function
                        
                        if (!whitePlayerTurn && currentMove.pieceColor == "b" && movesThatCauseCheck != temp /*&& Possible_moves.Count > 0*/)
                        {
                            Console.WriteLine(Possible_moves.Count);
                            Possible_moves.RemoveAt(Possible_moves.Count - 1);
                        }

                        if (IsInMinimax && movesThatCauseCheck != temp)//Đang sử dụng minimax
                        {
                            Minimax_moveCauseCheck++;
                        }
                        //Console.WriteLine("Bug");

                        startSquare.Tag = destination.Tag;              //the next few lines reset the board back to its state before the move 
                        destination.Tag = endSquareTitle;
                        if ((string)startSquare.Tag == "wKing")
                        {
                            whiteKingCol = currentMove.startCol;
                            whiteKingRow = currentMove.startRow;
                        }
                        else if ((string)startSquare.Tag == "bKing")
                        {
                            blackKingCol = currentMove.startCol;
                            blackKingRow = currentMove.startRow;
                        }
                    }
                }
            }
            return false;
        }
        private void testForCheck()
        {   //this function is analogous to scanForAvailableMoves() - it tests if a move causes check and increments the movesThatCauseCheck int
            //it does this my starting at the king and testing if it could reach any opponent pieces by using a move that the opponent piece could make
            //it delegates to subfunctions that do this, in a similar way to scanForAvailableMoves()

            pieceCausingCheck = null;           //initialise to null. This will return a piece causing check, if there is one. This is used to highlight that piece when a player
                                                //...has attempted a move that causes check
            
            //Quân vua nào đang bị check
            Control kingToCheck = gridTLP.GetControlFromPosition(whiteKingCol, whiteKingRow);

            if (!whitePlayerTurn)
            {
                kingToCheck = gridTLP.GetControlFromPosition(blackKingCol, blackKingRow);
            }


            int startRow = gridTLP.GetRow(kingToCheck);
            int startCol = gridTLP.GetColumn(kingToCheck); //Lấy vị trí của quân vua đang bị check
            string pieceTitle = (string)kingToCheck.Tag; //wKing
            string pieceColor = pieceTitle.Substring(0, 1);
            string pieceType = pieceTitle.Substring(1, pieceTitle.Length - 1);
            //Console.WriteLine("PieceType: " + pieceType);
            pictureBoxInformation currentPiece = new pictureBoxInformation(startCol, startRow, pieceType, pieceColor);
            checkSearch(currentPiece);
        }

        private void checkSearch(pictureBoxInformation currentMove /*Quân vua*/)
        {
            //Check các hướng có thể chiếu được quân vua 
            Debug.WriteLine("Here");
            //explore moving Orthogonally
            int offset = 1;
            //Xét hết các quân trên hướng ngang phải
            while (!foundCheck && exploreSquares(offset, 0, currentMove, "Ortho"))  //ortho denotes an orthogonal move
            {                       //the foundcheck bool is set to true when check is found - this prevents movesThatCauseCheck from being incremented more than once on the same square
                offset++;           //...such as when the king would be put in check my more than one opponent piece
            }
            offset = -1;
            //Xét hết các quân trên hướng ngang trái
            while (!foundCheck && exploreSquares(offset, 0, currentMove, "Ortho"))
            {
                offset--;
            }
            offset = 1;
            //Xét hết các quân trên hướng dọc trên
            while (!foundCheck && exploreSquares(0, offset, currentMove, "Ortho"))
            {
                offset++;
            }
            offset = -1;
            //Xét hết các quân trên hướng dọc dưới 
            while (!foundCheck && exploreSquares(0, offset, currentMove, "Ortho"))
            {
                offset--;
            }

            //explore moving diagonally

            offset = 1;
            while (!foundCheck && exploreSquares(offset, offset, currentMove, "Diag"))  //diag denotes an diagonal move
            {
                //Console.WriteLine("Chéo phải trên");
                offset++;
            }
            offset = 1;
            while (!foundCheck && exploreSquares(offset, -offset, currentMove, "Diag"))
            {
                //Console.WriteLine("Chéo phải dưới");
                offset++;
            }
            offset = 1;
            while (!foundCheck && exploreSquares(-offset, offset, currentMove, "Diag"))
            {
                //Console.WriteLine("Chéo trái trên");
                offset++;
            }
            offset = 1;
            while (!foundCheck && exploreSquares(-offset, -offset, currentMove, "Diag"))
            {
                //Console.WriteLine("Chéo trái dưới");
                offset++;
            }
            //explore knight moves

            for (int i = 0; i < 8; i++)
            {
                if (!foundCheck)
                {
                    exploreSquares(knightRowVector[i], knightColumnVector[i], currentMove, "Knight");   //knight denotes that we are testing a knight move
                }
                else { i = 8; }
            }

            //explore pawn moves
            if (!foundCheck)
            {
                if (currentMove.pieceColor == "w")
                {
                    exploreSquares(-1, -1, currentMove, "Pawn");                //pawn denotes that we are testing a knight move
                    exploreSquares(1, -1, currentMove, "Pawn");
                }
                else
                {
                    exploreSquares(-1, 1, currentMove, "Pawn");
                    exploreSquares(1, 1, currentMove, "Pawn");
                }
            }
            foundCheck = false;
        }

        private bool exploreSquares(int columnVector, int rowVector, pictureBoxInformation currentMove, string attackVulnerableTo /*Hướng tấn công*/)
        {
            //TODO: current move ở đây là quân Vua
            //TODO: các hướng vua có thể bị tấn công
            int col = currentMove.startCol + columnVector;          //the column containing the PictureBox we are testing
            int row = currentMove.startRow + rowVector;             //the row containing the PictureBox we are testing
            


            if (col >= 0 && col <= 7 && row >= 0 && row <= 7)                                           //if we are still on the board
            {
                //TODO: lấy ô dựa theo cột và hàng (col,row)
                Control destination = gridTLP.GetControlFromPosition(col, row);                         //get a handle on the cell that may be able to reach the king
                //TODO: nhận diện ô
                string destinationTag = (string)destination.Tag;
                //Màu hoặc ô trống (empty)
                string firstLetterOfDestinationTag = destinationTag.Substring(0, 1);
                //Loại cờ (nếu là quân)
                string destinationPieceType = destinationTag.Substring(1, destinationTag.Length - 1);   //e.g. 'Pawn', 'Rook', etc.

                if (firstLetterOfDestinationTag == "e")             //case empty cell
                {   //Ô trống
                    return true;                                    //square cannot attack king (nothing there!) but perhaps the next one can
                }
                else if (firstLetterOfDestinationTag != currentMove.pieceColor)         //found an opponent piece
                {   //Ô không trống

                    //Tấn công hướng dọc, ngang
                    if (attackVulnerableTo == "Ortho")              //function was called with argument 'Ortho' - vertical only or horizontal only movement from this square puts king in check
                    {
                        //Quân tấn công là xe hoặc hậu
                        if (destinationPieceType == "Rook" || destinationPieceType == "Queen")          //pieces that can move more than 1 square in that direction
                        {
                            Console.WriteLine(destinationPieceType + " cause check");
                            movesThatCauseCheck++;                              
                            pieceCausingCheck = (PictureBox)destination;    //get a handle on this so we can choose to highlight the piece causing check
                            foundCheck = true;           //important to change this to true - otherwise the calling function could count the king as being in check more than once for this move
                            return false;                //..for example when the king is in check by both a Rook and a Queen
                        }
                        //Quân tấn công là vua 
                        //TODO:vua chỉ đi được một ô 
                        else if (destinationPieceType == "King" && columnVector <= 1 && columnVector >= -1 && rowVector <= 1 && rowVector >= -1)    //king can only move one square in that direction
                        {
                            Console.WriteLine(destinationPieceType + " cause check");
                            movesThatCauseCheck++;
                            pieceCausingCheck = (PictureBox)destination;
                            foundCheck = true;
                            return false;
                        }
                    }
                    //Tấn công hướng chéo
                    if (attackVulnerableTo == "Diag")       
                    {
                        if (destinationPieceType == "Bishop" || destinationPieceType == "Queen")    //pieces that can move more than 1 square in that direction
                        {
                            Console.WriteLine(destinationPieceType + " cause check");
                            movesThatCauseCheck++;
                            pieceCausingCheck = (PictureBox)destination;
                            foundCheck = true;
                            return false;
                        }
                        else if (destinationPieceType == "King" && columnVector <= 1 && columnVector >= -1 && rowVector <= 1 && rowVector >= -1)    //king can only move one square in that direction
                        {
                            Console.WriteLine(destinationPieceType + " cause check");
                            movesThatCauseCheck++;
                            pieceCausingCheck = (PictureBox)destination;
                            foundCheck = true;
                            return false;
                        }
                    }
                    //Tấn công là ngựa
                    if (attackVulnerableTo == "Knight")                 //function was called with argument 'Knight' - we are looking at a square in which a knight could attack the selected king
                    {
                        if (destinationPieceType == "Knight")
                        {
                            Console.WriteLine(destinationPieceType + " cause check");
                            movesThatCauseCheck++;
                            pieceCausingCheck = (PictureBox)destination;
                            foundCheck = true;
                            return false;
                        }
                    }
                    //Tấn công là tốt
                    if (attackVulnerableTo == "Pawn")                   //diagonal moves where direction depends on the colour of the attacking pawn
                    {
                        if (destinationPieceType == "Pawn")
                        {
                            Console.WriteLine(destinationPieceType + " cause check");
                            movesThatCauseCheck++;
                            pieceCausingCheck = (PictureBox)destination;
                            foundCheck = true;
                            return false;
                        }
                    }
                    return false;
                }
            }
            return false;
        }

        private void unHighlightMoves()
        {                   //simply unpaint and square that has beeen painted a background color different to that it started with
            foreach (Control c in gridTLP.Controls)
            {
                if (c is PictureBox)
                {
                    if (c.BackColor != lightSquareColor && c.BackColor != darkSquareColor)      //if the square is highlighted a color other than the base color
                    {
                        if ((gridTLP.GetRow(c) % 2 == 0 && gridTLP.GetColumn(c) % 2 == 0) || (gridTLP.GetRow(c) % 2 == 1 && gridTLP.GetColumn(c) % 2 == 1))   //reset it to restore checkered color effect
                        {
                            c.BackColor = lightSquareColor;
                        }
                        else
                        {
                            c.BackColor = darkSquareColor;
                        }
                    }
                }
            }
        }
        private Control checkEndRows()
        {               //if a pawn is found in an endrow, return that pawn for promotion, otherwise return null
            for (int col = 0; col < 8; col++)
            {
                for (int row = 0; row < 8; row += 7)
                {
                    Control endSquareToCheck = gridTLP.GetControlFromPosition(col, row);

                    if ((string)endSquareToCheck.Tag == "wPawn" || (string)endSquareToCheck.Tag == "bPawn")
                    {
                        endSquareToCheck.BackColor = promotionPieceColor;    //highlight the pawn
                        return endSquareToCheck;
                    }
                }
            }
            return null;
        }
        private void switchtoPromotionMenu()
        {

            foreach (PictureBox c in promotionTLP.Controls)     //promotionTLP contains 4 PictureBox Controls: a knight, Rook, Bishop and a Queen
            {                                                   //...this loop makes sure the images and tags on those PictureBoxes match the current player
                string controlTag = (string)c.Tag;              //...to ensure their pawn is promoted to a piece of their own color 

                if (whitePlayerTurn)
                {
                    Console.WriteLine(c.Tag);

                    if (controlTag[0] == 'w')
                    {
                        c.Image = (System.Drawing.Bitmap)Properties.Resources.ResourceManager.GetObject((string)c.Tag);
                    }
                    else
                    {
                        c.Tag = 'w' + controlTag.Substring(1);
                        c.Image = (System.Drawing.Bitmap)Properties.Resources.ResourceManager.GetObject((string)c.Tag);
                    }
                }
                else
                {
                    if (controlTag[0] == 'b')
                    {
                        c.Image = (System.Drawing.Bitmap)Properties.Resources.ResourceManager.GetObject((string)c.Tag);
                    }
                    else
                    {
                        c.Tag = 'b' + controlTag.Substring(1);
                        c.Image = (System.Drawing.Bitmap)Properties.Resources.ResourceManager.GetObject((string)c.Tag);
                    }

                }
            }

            promotionTLP.Visible = true;                //display the promotion TLP PictureBoxes
            promotionTLP.Enabled = true;                //make them clickable
            piecePromotionLabel.Visible = true;         //display the text asking the player to choose a new piece
            gridTLP.Enabled = false;                    //disable the normal grid
            playerTurnLabel.Visible = false;            //make the other display items that are in the way of the pomotion information invisible
            movesLabel.Visible = false;                 //^
            gameTimeLabel.Visible = false;              //^


            if(whitePlayerTurn == false)
            {
                PictureBox selection = (PictureBox)promotionTLP.Controls[0];
                secondSelection.Tag = selection.Tag;        //secondSelection is a handle on the pawn they moved to the end - copy the details of the selected piece onto it
                secondSelection.Image = selection.Image;
                promotionTLP.Visible = false;               //hide and disable the promotionTLP and label
                promotionTLP.Enabled = false;
                piecePromotionLabel.Visible = false;

                gridTLP.Enabled = true;                     //re-enable and display the normal grid and labels
                playerTurnLabel.Visible = true;             //^
                movesLabel.Visible = true;                  //^
                gameTimeLabel.Visible = true;               //^
                whitePlayerTurn = !whitePlayerTurn;
            }

        }

        private void onPromotionClick(object sender, EventArgs e)
        {           //player has selected a piece they want to turn their pawn into
            PictureBox selection = sender as PictureBox;
            secondSelection.Tag = selection.Tag;        //secondSelection is a handle on the pawn they moved to the end - copy the details of the selected piece onto it
            secondSelection.Image = selection.Image;    //^

            promotionTLP.Visible = false;               //hide and disable the promotionTLP and label
            promotionTLP.Enabled = false;               
            piecePromotionLabel.Visible = false;

            gridTLP.Enabled = true;                     //re-enable and display the normal grid and labels
            playerTurnLabel.Visible = true;             //^
            movesLabel.Visible = true;                  //^
            gameTimeLabel.Visible = true;               //^
            whitePlayerTurn = !whitePlayerTurn;         //switch player back, ready for the next move
            movesThatCauseCheck = 0;                    //reset this for this to zero for the upcoming selectAllPieces()
            candidateMoves = 0;                         //^
            //Reset list vì sau khi white promotion sẽ tính lại các moves có thể đi được
            Possible_moves.Clear();
            selectAllPieces();                          //now need to test how many moves are possible for the opponent, and how many would cause check
            unHighlightMoves();
            if (movesThatCauseCheck == candidateMoves)
            {
                MessageBox.Show("CheckMate!");
                checkMateSequence();
            }
            if (whitePlayerTurn == false)
            {
                Console.WriteLine("Moves after white promotion: " + Possible_moves.Count);
                BotPlay();
            }

        }

        private void timer_Tick(object sender, EventArgs e)
        {               //a simple timer to display the elapsed game time
            timeInSeconds++;
            if (timeInSeconds < 60)
            {
                gameTimeLabel.Text = "Game time: " + timeInSeconds + "s";
            }
            else
            {
                gameTimeLabel.Text = "Game time: " + timeInSeconds / 60 + "m" + timeInSeconds % 60 + "s";
            }
        }
        private void quitButton_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }
        private void playAgainButton_Click(object sender, EventArgs e)
        {
            Application.Restart();
        }

        //Minimax
        
        private int BSIZE = 8;
        private int DefaultBeyondSteps = 3; //Should only be odd number


        public void printBoard(pictureBoxInformation[,] printedBoard)
        {
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    string title = printedBoard[i, j].pieceColor + printedBoard[i, j].pieceType;
                    Console.Write(title + "\t");
                }
                Console.WriteLine();
            }
        }


        public int totBoardEval(pictureBoxInformation[,] new_board, int move, int NumberOfPossibleMoves)
        {
            int score = 0;
            int scoreBlack = 0;
            int scoreWhite = 0;
            int material = boardEvalMaterial("w", new_board);
            scoreWhite += material;
            scoreWhite += boardEvalPositional(material,"w", new_board);

            material = boardEvalMaterial("b", new_board);
            scoreBlack  += material;
            scoreBlack  += boardEvalPositional(material,"b", new_board);

            score = scoreWhite - scoreBlack;
            //score > 0: White chiếm ưu thế 
            //score < 0: Black chiếm ưu thế 
            return (score + move * 50);
        }

        //Gía trị quân cờ
        public int boardEvalMaterial(string color, pictureBoxInformation[,] new_board)
        {
            int score = 0;
            int bishopNum = 0;

            for (int i = 0; i < BSIZE * BSIZE; i++)
            {
                int row = i / BSIZE;
                int col = i % BSIZE;
                switch (board[row,col].pieceType)
                {
                    case "Pawn":
                        {
                            if (new_board[row,col].pieceColor == color)
                            {
                                score += 100;
                            }
                            break;
                        }
                    case "Rook":
                        {
                            if (new_board[row, col].pieceColor == color)
                            {
                                score += 500;
                            }
                            break;
                        }
                    case "Knight":
                        {
                            if (new_board[row, col].pieceColor == color)
                            {
                                score += 320;
                            }
                            break;
                        }
                    case "Bishop":
                        {
                            if (new_board[row, col].pieceColor == color)
                            {
                                bishopNum += 1;
                            }
                            break;
                        }
                    case "Queen":
                        {
                            if (new_board[row, col].pieceColor == color)
                            {
                                score += 900;
                            }
                            break;
                        }
                }
            }
            if (bishopNum >= 2)
            {
                score += 330 * bishopNum;
            }
            else if (bishopNum == 1)
            { //if one bishop then penalty
                score += 250;
            }
            return score;
        }

        private int[,] WpawnBoard = {
        {0, 0, 0, 0, 0, 0, 0, 0},
        {50, 50, 50, 50, 50, 50, 50, 50},
        {10, 10, 20, 30, 30, 20, 10, 10},
        {5, 5, 10, 25, 25, 10, 5, 5},
        {0, 0, 0, 20, 20, 0, 0, 0},
        {5, -5, -10, 0, 0, -10, -5, 5},
        {5, 10, 10, -20, -20, 10, 10, 5},
        {0, 0, 0, 0, 0, 0, 0, 0}};
        private int[,] WrookBoard = {
        {0, 0, 0, 0, 0, 0, 0, 0},
        {5, 10, 10, 10, 10, 10, 10, 5},
        {-5, 0, 0, 0, 0, 0, 0, -5},
        {-5, 0, 0, 0, 0, 0, 0, -5},
        {-5, 0, 0, 0, 0, 0, 0, -5},
        {-5, 0, 0, 0, 0, 0, 0, -5},
        {-5, 0, 0, 0, 0, 0, 0, -5},
        {0, 0, 0, 5, 5, 0, 0, 0}};
        private int[,] WknightBoard = {
        {-50, -40, -30, -30, -30, -30, -40, -50},
        {-40, -20, 0, 5, 5, 0, -20, -40},
        {-30, 5, 10, 15, 15, 10, 5, -30},
        {-30, 0, 15, 20, 20, 15, 0, -30},
        {-30, 5, 15, 20, 20, 15, 5, -30},
        {-30, 0, 10, 15, 15, 10, 0, -30},
        {-40, -20, 0, 0, 0, 0, -20, -40},
        {-50, -40, -30, -30, -30, -30, -40, -50}};
        private int[,] WbishopBoard= {
        {-20, -10, -10, -10, -10, -10, -10, -20},
        {-10, 0, 0, 0, 0, 0, 0, -10},
        {-10, 0, 5, 10, 10, 5, 0, -10},
        {-10, 5, 5, 10, 10, 5, 5, -10},
        {-10, 0, 10, 10, 10, 10, 0, -10},
        {-10, 10, 10, 10, 10, 10, 10, -10},
        {-10, 5, 0, 0, 0, 0, 5, -10},
        {-20, -10, -10, -10, -10, -10, -10, -20}};
        private int[,] WqueenBoard = {
        {-20, -10, -10, -5, -5, -10, -10, -20},
        {-10, 0, 0, 0, 0, 0, 0, -10},
        {-10, 0, 5, 5, 5, 5, 0, -10},
        {-5, 0, 5, 5, 5, 5, 0, -5},
        {0, 0, 5, 5, 5, 5, 0, -5},
        {-10, 5, 5, 5, 5, 5, 0, -10},
        {-10, 0, 5, 0, 0, 0, 0, -10},
        {-20, -10, -10, -5, -5, -10, -10, -20}};
        private int[,] WkingMidBoard = {
        {-30, -40, -40, -50, -50, -40, -40, -30},
        {-30, -40, -40, -50, -50, -40, -40, -30},
        {-30, -40, -40, -50, -50, -40, -40, -30},
        {-30, -40, -40, -50, -50, -40, -40, -30},
        {-20, -30, -30, -40, -40, -30, -30, -20},
        {-10, -20, -20, -20, -20, -20, -20, -10},
        {20, 20, 0, 0, 0, 0, 20, 20},
        {20, 30, 10, 0, 0, 10, 30, 20}};
        private int[,] WkingEndBoard = {
        {-50, -40, -30, -20, -20, -30, -40, -50},
        {-30, -20, -10, 0, 0, -10, -20, -30},
        {-30, -10, 20, 30, 30, 20, -10, -30},
        {-30, -10, 30, 40, 40, 30, -10, -30},
        {-30, -10, 30, 40, 40, 30, -10, -30},
        {-30, -10, 20, 30, 30, 20, -10, -30},
        {-30, -30, 0, 0, 0, 0, -30, -30},
        {-50, -30, -30, -30, -30, -30, -30, -50}};


        //Black
        private int[,] BpawnBoard = {
        {0, 0, 0, 0, 0, 0, 0, 0},
        {5, 10, 10, -20, -20, 10, 10, 5},
        {5, -5, -10, 0, 0, -10, -5, 5},
        {0, 0, 0, 20, 20, 0, 0, 0},
        {5, 5, 10, 25, 25, 10, 5, 5},
        {10, 10, 20, 30, 30, 20, 10, 10},
        {50, 50, 50, 50, 50, 50, 50, 50},
        {0, 0, 0, 0, 0, 0, 0, 0}};
        private int[,] BrookBoard = {
        {0, 0, 0, 5, 5, 0, 0, 0},
        {-5, 0, 0, 0, 0, 0, 0, -5},
        {-5, 0, 0, 0, 0, 0, 0, -5},
        {-5, 0, 0, 0, 0, 0, 0, -5},
        {-5, 0, 0, 0, 0, 0, 0, -5},
        {-5, 0, 0, 0, 0, 0, 0, -5},
        {5, 10, 10, 10, 10, 10, 10, 5},
        {0, 0, 0, 0, 0, 0, 0, 0}};
        private int[,] BknightBoard = {
        {-50, -40, -30, -30, -30, -30, -40, -50},
        {-40, -20, 0, 0, 0, 0, -20, -40},
        {-30, 0, 10, 15, 15, 10, 0, -30},
        {-30, 5, 15, 20, 20, 15, 5, -30},
        {-30, 0, 15, 20, 20, 15, 0, -30},
        {-30, 5, 10, 15, 15, 10, 5, -30},
        {-40, -20, 0, 5, 5, 0, -20, -40},
        {-50, -40, -30, -30, -30, -30, -40, -50}};
        private int[,] BbishopBoard = {
        {-20, -10, -10, -10, -10, -10, -10, -20},
        {-10, 5, 0, 0, 0, 0, 5, -10},
        {-10, 10, 10, 10, 10, 10, 10, -10},
        {-10, 0, 10, 10, 10, 10, 0, -10},
        {-10, 5, 5, 10, 10, 5, 5, -10},
        {-10, 0, 5, 10, 10, 5, 0, -10},
        {-10, 0, 0, 0, 0, 0, 0, -10},
        {-20, -10, -10, -10, -10, -10, -10, -20},};
        private int[,] BqueenBoard = {
        {-20, -10, -10, -5, -5, -10, -10, -20},
        {-10, 0, 5, 0, 0, 0, 0, -10},
        {-10, 5, 5, 5, 5, 5, 0, -10},
        {0, 0, 5, 5, 5, 5, 0, -5},
        {-5, 0, 5, 5, 5, 5, 0, -5},
        {-10, 0, 5, 5, 5, 5, 0, -10},
        {-10, 0, 0, 0, 0, 0, 0, -10},
        {-20, -10, -10, -5, -5, -10, -10, -20}};
        private int[,] BkingMidBoard = {
        {20, 30, 10, 0, 0, 10, 30, 20},
        {20, 20, 0, 0, 0, 0, 20, 20},
        {-10, -20, -20, -20, -20, -20, -20, -10},
        {-20, -30, -30, -40, -40, -30, -30, -20},
        {-30, -40, -40, -50, -50, -40, -40, -30},
        {-30, -40, -40, -50, -50, -40, -40, -30},
        {-30, -40, -40, -50, -50, -40, -40, -30},
        {-30, -40, -40, -50, -50, -40, -40, -30}};
        private int[,] BkingEndBoard = {
        {-50, -30, -30, -30, -30, -30, -30, -50},
        {-30, -30, 0, 0, 0, 0, -30, -30},
        {-30, -10, 20, 30, 30, 20, -10, -30},
        {-30, -10, 30, 40, 40, 30, -10, -30},
        {-30, -10, 30, 40, 40, 30, -10, -30},
        {-30, -10, 20, 30, 30, 20, -10, -30},
        {-30, -20, -10, 0, 0, -10, -20, -30},
        {-50, -40, -30, -20, -20, -30, -40, -50}};

        //Gía trị vị trí
        public int boardEvalPositional(int material, string color, pictureBoxInformation[,] new_board)
        {
            int score = 0;

            for (int i = 0; i < BSIZE * BSIZE; i++)
            {
                int row = i / BSIZE;
                int col = i / BSIZE;
                switch (new_board[row, col].pieceType)
                {
                    case "Pawn":
                        {
                            if (new_board[row,col].pieceColor == color)
                            {
                                if (color == "w")
                                {
                                    score += WpawnBoard[row, col];
                                }
                                else
                                {
                                    score += BpawnBoard[row, col];
                                }
                                
                            }
                            break;
                        }
                    case "Rook":
                        { // rook
                            if (new_board[row, col].pieceColor == color)
                            {
                                if (color == "w")
                                {
                                    score += WrookBoard[row, col];
                                }
                                else
                                {
                                    score += BrookBoard[row, col];
                                }
                            }
                            break;
                        }
                    case "Knight":
                        { // knight
                            if (new_board[row, col].pieceColor == color)
                            {
                                if (color == "w")
                                {
                                    score += WknightBoard[row, col];
                                }
                                else
                                {
                                    score += BknightBoard[row, col];
                                }
                            }
                            break;
                        }
                    case "Bishop":
                        { // bishop
                            if (new_board[row, col].pieceColor == color)
                            {
                                if (color == "w")
                                {
                                    score += WbishopBoard[row, col];
                                }
                                else
                                {
                                    score += BbishopBoard[row, col];
                                }
                            }
                            break;
                        }
                    case "Queen":
                        { // queen
                            if (new_board[row, col].pieceColor == color)
                            {
                                if (color == "w")
                                {
                                    score += WqueenBoard[row, col];
                                }
                                else
                                {
                                    score += BqueenBoard[row, col];
                                }
                            }
                            break;
                        }
                    case "King":
                        { // king
                            if (material >= 1750)
                            {
                                if (color == "w")
                                {
                                    score += WkingMidBoard[row, col];
                                }
                                else
                                {
                                    score += BkingMidBoard[row, col];
                                }
                            }
                            else
                            {
                                if (color == "w")
                                {
                                    score += WkingEndBoard[row, col];
                                }
                                else
                                {
                                    score += BkingEndBoard[row, col];
                                }
                            }
                            break;
                        }
                }
            }
            return score;
        }

        //Test các nước đi trên bàn cờ giả
        public pictureBoxInformation[,] TestMove(pictureBoxInformation[,] cur_board, int next_row, int next_col,int cur_row, int cur_col, List<possible_move> tempList)
        {
            pictureBoxInformation[,] new_board = new pictureBoxInformation[8, 8];
            for(int i = 0; i < 8;  i++)
            {
                for(int j = 0; j < 8; j++)
                {
                    new_board[i, j] = new pictureBoxInformation(cur_board[i, j]);
                }
            }
            //Cập nhật bàn cờ mới
            int beforeMove_column = cur_col;
            int beforeMove_row = cur_row;
            string beforeMove_type = new_board[beforeMove_row, beforeMove_column].pieceType;
            string beforeMove_color = new_board[beforeMove_row, beforeMove_column].pieceColor;


            int afterMove_column = next_col;
            int afterMove_row = next_row;

            new_board[afterMove_row, afterMove_column].pieceType = beforeMove_type;
            new_board[afterMove_row, afterMove_column].pieceColor = beforeMove_color;
            new_board[afterMove_row, afterMove_column].startCol = afterMove_column;
            new_board[afterMove_row, afterMove_column].startRow = afterMove_row;

            new_board[beforeMove_row, beforeMove_column].startCol = beforeMove_column;
            new_board[beforeMove_row, beforeMove_column].startRow = beforeMove_row;
            new_board[beforeMove_row, beforeMove_column].pieceColor = "e";
            new_board[beforeMove_row, beforeMove_column].pieceType = "mpty";

            //Xóa move mới trên list possible moves tạm thời
            tempList.RemoveAt(0);
            return new_board;
        }

        possible_move findingMove = null;
        public int MiniMax(pictureBoxInformation[,] cur_board, bool IsMaxTurn, List<possible_move> tempList, int move, int alpha, int beta)
        {
            //printBoard(cur_board);
            int def_val;
            //All possible move with new board
            //List<possible_move> tempList = FindingAllMoves(IsMaxTurn, cur_board);
            //Console.WriteLine("Total next moves we have: " + tempList.Count);
            pictureBoxInformation[,] new_board;
            if (move == 0 || tempList.Count == 0) //Đạt tới giới hạn nhìn trước hoặc checkmate
            {
                //Console.WriteLine("Debug");
                return totBoardEval(cur_board, move, tempList.Count);
            }
            if (IsMaxTurn)
            {
                def_val = int.MinValue;
                move -= 1;
                foreach (possible_move NextPossibleMove in tempList.ToList())
                {
                    new_board = TestMove(cur_board, NextPossibleMove.next_row, NextPossibleMove.next_col, NextPossibleMove.current_piece.startRow, NextPossibleMove.current_piece.startCol, tempList);
                    
                    List<possible_move> OpponentNextPosMoves = FindingAllMoves(false,new_board);
                    int calling_nextMove = MiniMax(new_board, false, OpponentNextPosMoves, move, alpha, beta);
                    def_val = Math.Max(def_val, calling_nextMove);
                    alpha = Math.Max(alpha, def_val);
                    if (beta <= alpha)
                    {
                        break;
                    }
                }
                
                return def_val;
            }
            else
            {
                def_val = int.MaxValue;
                move -= 1;
                foreach (possible_move NextPossibleMove in tempList.ToList())
                {
                    new_board = TestMove(cur_board, NextPossibleMove.next_row, NextPossibleMove.next_col, NextPossibleMove.current_piece.startRow, NextPossibleMove.current_piece.startCol, tempList);
                    
                    int test = def_val;
                    //Console.WriteLine("Before: " + tempList.Count);
                    List<possible_move> OpponentNextPosMoves = FindingAllMoves(true, new_board);
                    int calling_nextMove = MiniMax(new_board, true, OpponentNextPosMoves, move, alpha, beta);
                    //Console.WriteLine("After: " + tempList.Count);
                    def_val = Math.Min(def_val, calling_nextMove);
                    beta = Math.Min(beta, def_val);
                    if (test > calling_nextMove && test != int.MaxValue && IsBackToCurrentBoard(cur_board))
                    {
                        findingMove = NextPossibleMove;
                    }
                    if (beta <= alpha)
                    {
                        break;
                    }
                }
                return def_val;
            }
        }

        public bool IsBackToCurrentBoard(pictureBoxInformation[,] checking_board)
        {
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    string cur_piece = board[i, j].pieceColor + board[i, j].pieceType;
                    string checking_piece = checking_board[i, j].pieceColor + checking_board[i, j].pieceType;
                    if (!checking_piece.Equals(cur_piece))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private int Total_moveCanPlay = 0;
        private int Total_moveCanCause = 0;
        private int Minimax_moveCanPlay = 0;
        private int Minimax_moveCauseCheck = 0;
        private bool IsInMinimax = false;
        private List<int> nextMoves = new List<int>();
        public List<possible_move> FindingAllMoves(bool IsMaxTurn, pictureBoxInformation[,] checking_board) 
        {
            //printBoard(checking_board);
            List<possible_move> possible_M = new List<possible_move>();
            IsInMinimax = true;
            for (int i = 0; i<8;i++)
            {
                for (int j=0; j<8;j++)
                {
                    if (IsMaxTurn)
                    {
                        if (checking_board[i,j].pieceColor == "w")
                        {
                            findReachableSquares(checking_board[i, j]);
                            if (Minimax_moveCanPlay - Minimax_moveCauseCheck != 0) //This movement wont cause check --> add
                            {
                                foreach (int item in nextMoves)
                                {
                                    possible_move move = new possible_move(checking_board[i, j], item % BSIZE, item / BSIZE);
                                    possible_M.Add(move);
                                }
                                
                            }
                        }
                    }
                    else
                    {
                        if (checking_board[i, j].pieceColor == "b")
                        {
                            //Console.WriteLine("Test: "+ checking_board[i, j].pieceColor + checking_board[i, j].pieceType);
                            findReachableSquares(checking_board[i, j]);
                            //Console.WriteLine("Minimax_moveCanPlay: {0}, Minimax_moveCauseCheck: {1}", Minimax_moveCanPlay, Minimax_moveCauseCheck);
                            if (Minimax_moveCanPlay - Minimax_moveCauseCheck != 0) //This movement wont cause check --> add
                            {
                                
                                foreach (int item in nextMoves)
                                {
                                    //Console.WriteLine("Add: {0}[{1},{2}]", checking_board[i, j].pieceColor + checking_board[i, j].pieceType, item / BSIZE, item % BSIZE);
                                    possible_move move = new possible_move(checking_board[i, j], item % BSIZE, item / BSIZE);
                                    possible_M.Add(move);
                                }
                            }
                        }
                    }
                    Minimax_moveCanPlay = 0;
                    Minimax_moveCauseCheck = 0;
                    nextMoves = new List<int>();
                }
            }
            return possible_M;
        }
    }
}
