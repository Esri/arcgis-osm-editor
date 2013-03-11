using System;
using System.Collections.Generic;
using System.Text;

namespace ServicePublisher.GeoSpatial
{
    public class SdeServerInfo
    {
        private string _server;
        public string server
        {
            get { return _server; }
            set { _server = value; }
        }

        private string _instance;
        public string instance
        {
            get { return _instance; }
            set { _instance = value; }
        }

        private string _database;
        public string database
        {
            get { return _database; }
            set { _database = value; }
        }

        private string _user;
        public string user
        {
            get { return _user; }
            set { _user = value; }
        }

        private string _password;
        public string password
        {
            get { return _password; }
            set { _password = value; }
        }

        private string _version;
        public string version
        {
            get { return _version; }
            set { _version = value; }
        }
    }
}
