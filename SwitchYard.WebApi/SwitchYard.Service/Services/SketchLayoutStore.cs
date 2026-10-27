using System.Text.Json;
using SwitchYard.SketchLayout;

namespace SwitchYard.Service.Services;

public sealed class SketchLayoutStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private DBConnector Db => DBConnector.GetDBConnector(DBConnector.SketchLayoutDatabaseSectionName);
    public sealed class Row
    {
        public string Id { get; set; } = "";
        public string OwnerId { get; set; } = "";
        public string OwnerName { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public int Revision { get; set; }
        public string CreatedAt { get; set; } = "";
        public string UpdatedAt { get; set; } = "";
        public string DocumentJson { get; set; } = "";
        public SchemeDetail Detail() => new(Id, OwnerId, OwnerName, Revision, CreatedAt, UpdatedAt, JsonSerializer.Deserialize<SketchLayoutScheme>(DocumentJson, Json)!);
        public SchemeSummary Summary() => new(Id, OwnerId, OwnerName, Name, Description, Revision, CreatedAt, UpdatedAt);
    }
    public List<SchemeSummary> List(string ownerId, bool admin) => (Db.Query<Row>("SELECT Id,OwnerId,OwnerName,Name,Description,Revision,CreatedAt,UpdatedAt FROM sketchlayoutscheme WHERE OwnerId=@ownerId OR @admin=1 ORDER BY UpdatedAt DESC,Id", new { ownerId, admin = admin ? 1 : 0 }) ?? []).Select(r => r.Summary()).ToList();
    public SchemeDetail? Get(string id, string ownerId, bool admin) => Db.Query<Row>("SELECT * FROM sketchlayoutscheme WHERE Id=@id AND (OwnerId=@ownerId OR @admin=1)", new { id, ownerId, admin = admin ? 1 : 0 })?.FirstOrDefault()?.Detail();
    public SchemeDetail Create(SketchLayoutScheme document, string ownerId, string ownerName)
    {
        Validation.Validate(document);
        document.AnalysisSettings ??= document.ResolveAnalysisSettings();
        var id = Guid.NewGuid().ToString("N"); var now = DateTime.UtcNow.ToString("O");
        Db.ExecuteNonQuery("INSERT INTO sketchlayoutscheme(Id,OwnerId,OwnerName,Name,Description,Revision,CreatedAt,UpdatedAt,DocumentJson) VALUES(@id,@ownerId,@ownerName,@Name,@Description,1,@now,@now,@json)", new { id, ownerId, ownerName, document.Name, document.Description, now, json = JsonSerializer.Serialize(document, Json) });
        return new(id, ownerId, ownerName, 1, now, now, document);
    }
    public bool Update(string id, string ownerId, bool admin, SchemeWriteRequest request, string? updatedAt = null)
    {
        Validation.Validate(request.Document);
        request.Document.AnalysisSettings ??= request.Document.ResolveAnalysisSettings();
        return Db.ExecuteNonQuery("UPDATE sketchlayoutscheme SET Name=@Name,Description=@Description,DocumentJson=@json,Revision=Revision+1,UpdatedAt=@now WHERE Id=@id AND Revision=@Revision AND (OwnerId=@ownerId OR @admin=1)", new { id, ownerId, admin = admin ? 1 : 0, request.Revision, request.Document.Name, request.Document.Description, json = JsonSerializer.Serialize(request.Document, Json), now = updatedAt ?? DateTime.UtcNow.ToString("O") }) == 1;
    }
    public bool Delete(string id, string ownerId, bool admin, int revision) => Db.ExecuteNonQuery("DELETE FROM sketchlayoutscheme WHERE Id=@id AND Revision=@revision AND (OwnerId=@ownerId OR @admin=1)", new { id, ownerId, admin = admin ? 1 : 0, revision }) == 1;
}
