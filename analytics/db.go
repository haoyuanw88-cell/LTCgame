package analytics

import "encore.dev/storage/sqldb"

// db is an Encore-managed PostgreSQL database. Encore applies migrations on
// startup locally and during deployment, and injects the connection settings.
var db = sqldb.NewDatabase("ltc", sqldb.DatabaseConfig{
	Migrations: "./migrations",
})
