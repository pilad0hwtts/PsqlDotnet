using System;
using Npgsql;
//TODO: More atteributes
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace TestPostgres {
    //Большая часть команд требует привилегий суперпользователя
    //TODO: LOG conventionals?
    static class NpgsqlUtils {

        public static NpgsqlConnection Execute (this NpgsqlConnection connection, string sqlCommand) {
            Log.Information ("Execute {command}", sqlCommand);
            using (var command = connection.CreateCommand (sqlCommand)) {
                command.ExecuteNonQuery ();
            }
            return connection;
        }
        public static NpgsqlCommand CreateCommand (this NpgsqlConnection connection, string command) => new NpgsqlCommand (command, connection);
        public static NpgsqlConnection Open (this NpgsqlConnection connection) {
            connection.Open ();
            return connection;
        }
        public class CreateUserOptions {
            public string Username { get; set; } = "postgres";
            public bool Encrypted { get; set; } = true;
            public string Password { get; set; } = null;

            public bool CreateDb { get; set; } = false;
            public bool CreateUser { get; set; } = false;
            public DateTime? ValidUntil { get; set; } = null;
            //TODO: SysID            
        };
        public static NpgsqlConnection CreateUser (this NpgsqlConnection connection, CreateUserOptions opt) {
            Log.Information ("[ngpsql_utils] Creating {user}", opt.Username);

            string commandQuery = $@"CREATE USER {opt.Username} 
                WITH  
                {(opt.CreateDb ? "CREATEDB " : "")}
                {(opt.CreateUser ? "CREATEUSER " : "")}
                {(opt.Password != null ? opt.Encrypted ? $"ENCRYPTED PASSWORD '{opt.Password}' " : $"UNENCRYPTED PASSWORD '{opt.Password}' " : "")}
                {(opt.ValidUntil != null ? $"VALID UNTIL {opt.ValidUntil} " : "")}";

            using (var command = connection.CreateCommand (commandQuery)) {
                command.ExecuteNonQuery ();
            }

            return connection;
        }

        //TODO: AlterUser, List Users?

        public static NpgsqlConnection DropUser (this NpgsqlConnection connection, string username) {
            Log.Information ("[ngpsql_utils] Dropping {user}", username);

            string commandQuery = $"DROP USER {username}";
            using (var command = connection.CreateCommand (commandQuery)) {
                command.ExecuteNonQuery ();
            }
            return connection;
        }

        public class CreateDatabaseOptions {
            public string Name { get; set; } = "postgres";
            public string Owner { get; set; } = "DEFAULT";
            public string Template { get; set; } = "DEFAULT";
            public string Encoding { get; set; } = "DEFAULT";
            public string Tablespace { get; set; } = "DEFAULT";
            [Range (1, int.MaxValue, ErrorMessage = "Connection limit must be from -1")]
            public int ConnectionLimit { get; set; } = -1;
            public bool IsTemplate { get; set; } = false;
            public bool AllowConnection { get; set; } = true;

            public string LocaleCollate { get; set; } = null;
            public string LocaleType { get; set; } = null;

        };

        public static NpgsqlConnection CreateDatabase (this NpgsqlConnection connection, CreateDatabaseOptions opt) {
            Log.Information ("Creating database {database}", opt.Name);
            string commandQuery = $@"CREATE DATABASE {opt.Name} 
            WITH OWNER = {opt.Owner} TEMPLATE = {opt.Template} Encoding = {opt.Encoding} TABLESPACE = {opt.Tablespace}
            ALLOW_CONNECTIONS = {opt.AllowConnection} CONNECTION LIMIT = {opt.ConnectionLimit} IS_TEMPLATE = {opt.IsTemplate}
            { (opt.LocaleCollate != null ? $" LC_COLLATE = '{opt.LocaleCollate}'" : "") } 
             { (opt.LocaleType != null ? $" LC_CTYPE  = '{opt.LocaleType}'" : "") }
            
            ";
            Log.Verbose (commandQuery);
            using (var command = connection.CreateCommand (commandQuery)) {
                command.ExecuteNonQuery ();
            }
            return connection;
        }

        public static NpgsqlConnection DropDatabase (this NpgsqlConnection connection, string name, bool ifExists) {
            Log.Information ("Dropping database {database}", name);
            string commandQuery = $"DROP DATABASE {(ifExists ? "IF EXISTS" : "")} {name}";
            Log.Verbose (commandQuery);
            using (var command = connection.CreateCommand (commandQuery)) {
                command.ExecuteNonQuery ();
            }
            return connection;
        }

    }

}