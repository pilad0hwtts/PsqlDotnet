using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.IO;
using System;
using System.Security;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Serilog;


//TODO: Normal namespace naming?
namespace PsqlDotnet
{
    //TODO: Адаптировать к загрузчику (записать все что неудобно сейчас и сделать TODOшки)
    //TODO: Подумать/посмотрть над возможной интеграции работы с геоданными и с растрами

    [Table("work", Schema = "total")]
    public class Work
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int id { get; set; }
        public string first { get; set; }
        public string second { get; set; }
    }

    public class Contex : DbContext
    {
        public DbSet<Work> Works { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options) => options.UseNpgsql(new Npgsql.NpgsqlConnectionStringBuilder
        {
            Host = "127.0.0.1",
            Database = "testdatabase",
            Username = "pilad",
            Password = "pilad"
        }.ToString());
    }

    class Program
    {
        static void Main(string[] args)
        {            
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger();

            Log.Information("Start testing PostgreConfigurer");
            var workDirectory = Path.Combine(Directory.GetParent(Directory.GetCurrentDirectory()).FullName, "postgres-oracle");


            string username;
            SecureString password;
            Console.WriteLine("Username: ");
            username = Console.ReadLine();
            Console.WriteLine("Enter password: ");
            password = GetPassword();

            using var conf = new PostgresqlAppManager(workDirectory, new PostgresqlAppManager.User(username, password));           
            using var ps = new PostgisManager(conf);

            bool isForceInstall = args.Any(x => x == "force");

            if (isForceInstall || conf.IsInstalled) {
                if (conf.IsRunning)
                    conf.StopPostgres();
                conf.InstallPostgreSql();
                ps.InstallPostgis();
            }

            var sudoUser = new Npgsql.NpgsqlConnectionStringBuilder
            {
                Host = "127.0.0.1",
                Database = "postgres",
                Username = "postgres",
                Password = "postgres"
            };

            if (!conf.IsRunning)
            {
                conf.RunPostgres();
            }

            using (var contex = new Contex())
            {

                bool isDbReady = !args.Any(x => x == "full_recreate");

                //FIXME: Не работает
                /*try {
                    isDbReady &= contex.Database.CanConnect ();
                } catch {
                    isDbReady = false;
                }*/

                if (!isDbReady)
                {
                    Log.Information("Need recreation of database");
                    using (var connection = new Npgsql.NpgsqlConnection(sudoUser.ToString()))
                    {
                        connection.Open();
                        connection
                            .DropDatabase("TestDatabase", true)
                            .CreateUser(new NpgsqlUtils.CreateUserOptions
                            {
                                Username = "pilad",
                                Password = "pilad"
                            })
                            .CreateDatabase(new NpgsqlUtils.CreateDatabaseOptions
                            {
                                Name = "TestDatabase",
                                Owner = "pilad",

                            });                        
                        contex.Database.EnsureCreated();    
                        connection.ChangeDatabase("testdatabase");
                        connection.Execute(ps.ActivatePostgisSql());
                        //TODO: Is nesessary?
                        connection.Close();
                    }
                    Log.Information("Recreating full scheme contex...");                                        
                }

                contex.Works.Add(new Work { first = "123", second = "dsa" });
                contex.SaveChanges();

                foreach (var work in contex.Works)
                {
                    Log.Information("{first} {second}", work.first, work.second);

                }
            }

            //conf.StopPostgres();

            System.Threading.Thread.Sleep (1000);
            
            
        }
        public static SecureString GetPassword()
        {
            SecureString securePwd = new SecureString();
            ConsoleKeyInfo key;
            do
            {
                key = Console.ReadKey(true);

                // Ignore any key out of range.
                if (((int)key.Key) >= 65 && ((int)key.Key <= 90))
                {
                    // Append the character to the password.
                    securePwd.AppendChar(key.KeyChar);
                    Console.Write("*");
                }
                // Exit if Enter key is pressed.
            } while (key.Key != ConsoleKey.Enter);
            return securePwd;
        }
    }
}