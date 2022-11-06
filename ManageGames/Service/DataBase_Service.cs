using ManageGames.Models;
using System.Data.SQLite;

namespace ManageGames.Service
{
    public class DataBase_Service
    {

        private SQLiteConnection sqlite;

        public DataBase_Service()
        {
            //This part killed me in the beginning.  I was specifying "DataSource"
            //instead of "Data Source"
            var path = Directory.GetCurrentDirectory() + "\\DB\\DataBase.db";
            if(!File.Exists(path))
            {
                if (!Directory.Exists(Directory.GetCurrentDirectory() + "\\DB")) Directory.CreateDirectory(Directory.GetCurrentDirectory() + "\\DB\\");
                
                SQLiteConnection.CreateFile(path);
                //File.Create(path);
                sqlite = new SQLiteConnection("Data Source = " + path);
                sqlite.Open();

                string console_table_query  = "CREATE TABLE tblConsoles (ID INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL," +
                                                                        "ConsoleName VARCHAR," +
                                                                        "Company INTEGER REFERENCES tblCompanies (ID));";
                string games_table_query    = "CREATE TABLE tblGames (ID INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL," +
                                                                  "Console INTEGER REFERENCES tblConsoles(ID)," +
                                                                  "GameName VARCHAR," +
                                                                  "Anzahl_von_Game INTEGER," +
                                                                  "Wunschliste BOOLEAN," +
                                                                  "UserID GUID REFERENCES tblUser (UserID));";
                
                string user_table_query     = "CREATE TABLE tblUser (UserID GUID NOT NULL PRIMARY KEY," +
                                                                "ProfilePicturesID GUID," +
                                                                "Username VARCHAR(40)," +
                                                                "Password VARCHAR(16)," +
                                                                "IsAdmin BOOLEAN," +
                                                                "CookieID VARCHAR(10)); ";

                string company_table_query = "CREATE TABLE tblCompanies (ID INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL," +
                                                                        "CompanyName VARCHAR);";


                new SQLiteCommand(company_table_query, sqlite).ExecuteNonQuery();
                new SQLiteCommand("INSERT INTO tblCompanies(ID,CompanyName) VALUES(" + 0 + ",'NoCompany');", sqlite).ExecuteNonQuery();
                new SQLiteCommand(console_table_query, sqlite).ExecuteNonQuery();
                new SQLiteCommand("INSERT INTO tblConsoles(ID,ConsoleName,Company) VALUES(" + 0 + ",'NoConsole', 0);", sqlite).ExecuteNonQuery();
                new SQLiteCommand(user_table_query, sqlite).ExecuteNonQuery();
                new SQLiteCommand(games_table_query, sqlite).ExecuteNonQuery();
                sqlite.Close();
                CreateUser("user", "password1", true);
            }
            else
            {
                sqlite = new SQLiteConnection("Data Source = " + path);
            }
        }

        #region WriteToDB
        
        public void DeleteGame(int id)
        {
            sqlite.Open();

            var command = sqlite.CreateCommand();

            command.CommandText =
                "DELETE FROM tblGames " +
                "WHERE ID = '" + id + "'";

            command.ExecuteNonQuery();

            sqlite.Close();
        }
        public void AddGame(string gameName, int game_amount, int console, string wishlist, string userID)
        {
            sqlite.Open();

            var command = sqlite.CreateCommand();

            command.CommandText =
                "Insert into tblGames (Console , GameName, Anzahl_von_Game, Wunschliste, UserID  )" +
                "Values('" + console + "','" + gameName + "','" + game_amount + "','" + (wishlist==null? false : true) + "','"+ userID +" ')";
            command.ExecuteNonQuery();

            sqlite.Close();
        }
        public void AddCategory(string categoryName)
        {
            sqlite.Open();

            var command = sqlite.CreateCommand();

            command.CommandText =
                "Insert into tblConsoles (ConsoleName)" +
                "Values('" + categoryName + "')";
            command.ExecuteNonQuery();

            sqlite.Close();
        }
        public void UpdateGame(string gameName, int game_amount, int console, string wishlist, int id)
        {
            sqlite.Open();

            var command = sqlite.CreateCommand();

            command.CommandText =
                "UPDATE tblGames " +
                "SET " +
                "Console = '" + console + "'," +
                "GameName = '" + gameName + "'," +
                "Anzahl_von_Game = '" + game_amount + "'," +
                "Wunschliste = '" + (wishlist == null ? false : true) + "'"+
                "WHERE ID = '"+id+"'";

            command.ExecuteNonQuery();

            sqlite.Close();
        }
        public void UpdateCategory(int category_id, string categroyName)
        {
            sqlite.Open();

            var command = sqlite.CreateCommand();

            command.CommandText =
                "UPDATE tblConsoles " +
                "SET " +
                "ConsoleName = '" + categroyName + "'" +
                "WHERE ID = '" + category_id + "'";

            command.ExecuteNonQuery();

            sqlite.Close();
        }
        public void DeleteCategory(int id)
        {
            sqlite.Open();

            var command = sqlite.CreateCommand();

            command.CommandText =
                "DELETE FROM tblConsoles " +
                "WHERE ID = '" + id + "'";

            command.ExecuteNonQuery();

            sqlite.Close();
            ChangeCategoryfromManyGames(id);

        }
        public void ChangeCategoryfromManyGames(int old_id, int new_id=1)
        {
            sqlite.Open();

            var command = sqlite.CreateCommand();

            command.CommandText =
                "UPDATE tblGames " +
                "SET " +
                "Console = '" + new_id + "'" +
                "WHERE Console = '" + old_id + "'";

            command.ExecuteNonQuery();

            sqlite.Close();
        }
        public void CreateUser(string username, string password, bool isAdmin)
        {
            sqlite.Open();

            var command = sqlite.CreateCommand();

            command.CommandText =
                "Insert into tblUser (UserID,ProfilePicturesID, Username, Password, IsAdmin, CookieID )" +
                "Values('" + Guid.NewGuid() + "','"+ Guid.Empty + "','"+ username + "','"+ password + "','"+ isAdmin + "','StartCook')";
            command.ExecuteNonQuery();

            sqlite.Close();
        }
        public void ChangeCookieId(Guid userID, string cookieID)
        {
            sqlite.Open();

            var command = sqlite.CreateCommand();

            command.CommandText =
               @"Update tblUser " +
               "Set CookieID ='" + cookieID + "'" +
               "Where UserID = '" + userID + "'";
            ;
            command.ExecuteNonQuery();

            sqlite.Close();
        }

        #endregion

        #region ReadFromDB

        public List<CompanyModel> GetCompanies()
        {
            List<CompanyModel> companies = new List<CompanyModel>();
            sqlite.Open();

            var command = sqlite.CreateCommand();
            command.CommandText =
                @"
                SELECT *
                FROM tblCompanies
            ";

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    companies.Add(
                        new CompanyModel
                        {
                           CompanyId    = reader.GetInt32(0),
                           CompanyName  = reader.GetString(1) 
                        }
                    );
                }
            }
            sqlite.Close();
            return companies;
        }
        public GameModel GetSingleGame(int ID)
        {
            GameModel Game = new GameModel();
            List<ConsoleModel> consoleModels = GetCategoryList();
            sqlite.Open();

            var command = sqlite.CreateCommand();
            command.CommandText =
                @"
                SELECT *
                FROM tblGames
                Where ID = "+ID;

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {

                    Game.GameId = reader.GetInt32(0);
                    Game.Console = consoleModels.Where(x => x.ConsoleId.Equals(reader.GetInt32(1))).First();
                    Game.GameName = reader.GetString(2);
                    Game.Anzah_von_Game = reader.GetInt32(3);
                    Game.IsOnWishList = Convert.ToBoolean(reader.GetString(4));
                        
                }
            }
            sqlite.Close();
            return Game;
        }
        public ConsoleModel GetSingleConsole(int ID)
        {
            ConsoleModel Console = new ConsoleModel();
            sqlite.Open();

            var command = sqlite.CreateCommand();
            command.CommandText =
                @"
                SELECT *
                FROM tblConsoles
                Where ID = " + ID;

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    Console.ConsoleId = reader.GetInt32(0);
                    Console.Console_Name = reader.GetString(1);
                }
            }
            sqlite.Close();
            return Console;
        }
        public List<GameModel> GetGameList()
        {
            List<GameModel> GameList = new List<GameModel>();
            List<UserModel> userModels = GetUserList();
            List<ConsoleModel> consoleModels = GetCategoryList();
            sqlite.Open();

            var command = sqlite.CreateCommand();
            command.CommandText =
                @"
                SELECT *
                FROM tblGames
            ";

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    GameList.Add(
                        new GameModel
                        {
                            GameId = reader.GetInt32(0),
                            Console = consoleModels.Where(x=>x.ConsoleId.Equals(reader.GetInt32(1))).First(),
                            GameName = reader.GetString(2),
                            Anzah_von_Game = reader.GetInt32(3),
                            IsOnWishList = Convert.ToBoolean(reader.GetString(4)),
                            User = userModels.Where(x => x.UserID.Equals(reader.GetGuid(5))).First()
                        }
                    );
                }
            }
            sqlite.Close();
            return GameList;
        }
        public List<ConsoleModel> GetCategoryList()
        {
            List<ConsoleModel> consolemodel = new List<ConsoleModel>();
            List<CompanyModel> companies    = GetCompanies();
            sqlite.Open();



            var command = sqlite.CreateCommand();
            command.CommandText =
                @"
                SELECT *
                FROM tblConsoles
            ";

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    consolemodel.Add(
                        new ConsoleModel
                        {
                            ConsoleId = reader.GetInt32(0),
                            Console_Name = reader.GetString(1),
                            Company = companies.Where(x => x.CompanyId.Equals(reader.GetInt32(2))).First()
                        }
                        ); ;

                }
            }
            sqlite.Close();
            
            return consolemodel; 
        }
        public List<UserModel> GetUserList()
        {
            List<UserModel> usermodel = new List<UserModel>();
            sqlite.Open();



            var command = sqlite.CreateCommand();
            command.CommandText =
                @"
                SELECT *
                FROM tblUser
            ";

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    usermodel.Add(
                        new UserModel
                        {
                            UserID = reader.GetGuid(0),
                            ProfilePicturesID = reader.GetGuid(1),
                            Username = reader.GetString(2),
                            Password = reader.GetString(3),
                            IsAdmin = Convert.ToBoolean(reader.GetString(4)),
                            CookieID = string.IsNullOrWhiteSpace(reader.GetString(5)) ? " " : reader.GetString(5)                          
                        }
                        );

                }
            }
            sqlite.Close();

            return usermodel.OrderBy(x => x.Username).ToList();
        }

        
        #endregion

    }
}
