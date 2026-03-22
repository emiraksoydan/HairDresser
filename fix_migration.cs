// Bu dosyayı çalıştır: dotnet script fix_migration.cs
// Ya da pgAdmin'de şu SQL'i çalıştır:
// INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
// VALUES ('20260214162830_mig-1', '9.0.0') ON CONFLICT ("MigrationId") DO NOTHING;
// Sonra: dotnet ef database update --project DataAccess --startup-project Api
