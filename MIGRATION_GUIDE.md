# 🗄️ Database Migration Rehberi

## UserFcmToken Migration

Firebase Push Notification entegrasyonu için yeni bir tablo eklenmesi gerekiyor.

### Migration Oluşturma

```bash
cd C:\Users\yazilimciemir\source\repos\HairDresser
dotnet ef migrations add AddUserFcmToken --project DataAccess --startup-project Api
```

### Migration İçeriği (Beklenen)

```csharp
public partial class AddUserFcmToken : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "UserFcmTokens",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FcmToken = table.Column<string>(type: "nvarchar(max)", nullable: false),
                DeviceId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Platform = table.Column<string>(type: "nvarchar(max)", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserFcmTokens", x => x.Id);
                table.ForeignKey(
                    name: "FK_UserFcmTokens_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_UserFcmTokens_FcmToken",
            table: "UserFcmTokens",
            column: "FcmToken",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_UserFcmTokens_UserId_IsActive",
            table: "UserFcmTokens",
            columns: new[] { "UserId", "IsActive" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "UserFcmTokens");
    }
}
```

### Migration Uygulama

```bash
dotnet ef database update --project DataAccess --startup-project Api
```

### Rollback (Gerekirse)

```bash
dotnet ef database update PreviousMigrationName --project DataAccess --startup-project Api
```

## ⚠️ Önemli Notlar

1. **Backup**: Migration öncesi database backup alın
2. **Test**: Önce test ortamında deneyin
3. **Indexes**: Performance için index'ler otomatik oluşturulacak
4. **Foreign Key**: User silindiğinde FCM token'ları da silinecek (Cascade)

## ✅ Migration Sonrası Kontrol

```sql
-- Tablo oluşturuldu mu?
SELECT * FROM UserFcmTokens;

-- Index'ler var mı?
SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('UserFcmTokens');

-- Foreign key var mı?
SELECT * FROM sys.foreign_keys WHERE referenced_object_id = OBJECT_ID('Users');
```
