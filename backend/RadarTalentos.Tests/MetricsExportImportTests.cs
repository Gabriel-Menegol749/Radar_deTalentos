using System.Net;
using System.Text.Json;
using ClosedXML.Excel;

namespace RadarTalentos.Tests;

[Collection("api")]
public class MetricsExportImportTests(ApiFactory factory)
{
    [Fact]
    public async Task Metricas_usam_historico_e_respeitam_filtro_por_vaga()
    {
        var c = await factory.LoginAsync();
        var company = await Seed.Company(c);
        var job = await Seed.Job(c, company.Id(), openedAt: DateTime.Today.AddDays(-10).ToString("yyyy-MM-dd"));
        var otherJob = await Seed.Job(c, company.Id());

        var a1 = await Seed.Application(c, (await Seed.Candidate(c)).Id(), job.Id(), "Hunting");
        var a2 = await Seed.Application(c, (await Seed.Candidate(c)).Id(), job.Id(), "Gupy");
        var a3 = await Seed.Application(c, (await Seed.Candidate(c)).Id(), job.Id(), "Hunting");
        await Seed.Application(c, (await Seed.Candidate(c)).Id(), otherJob.Id());

        await c.PostJson($"/api/applications/{a1.Id()}/move", new { targetStage = "Entrevista" });
        await c.PostJson($"/api/applications/{a1.Id()}/move", new { targetStage = "Aprovação", offeredSalary = 10000, requestedSalary = 12000 });
        await c.PostJson($"/api/applications/{a2.Id()}/move", new { targetStage = "Entrevista" });
        await c.PostJson($"/api/applications/{a2.Id()}/resolve", new { status = "Declined" });
        await c.PostJson($"/api/applications/{a1.Id()}/move", new { targetStage = "Etapa interna" });
        await c.PostJson($"/api/applications/{a1.Id()}/move", new { targetStage = "Case" });
        await c.PostJson($"/api/applications/{a1.Id()}/move", new { targetStage = "Proposta" });
        await c.PostJson($"/api/applications/{a1.Id()}/resolve", new { status = "Hired" }); // a3 vira Rejected (D6)

        var m = await c.GetJson($"/api/metrics?jobId={job.Id()}");
        Assert.Equal(3, m.GetProperty("totalAccessed").GetInt32());
        Assert.Equal(2, m.GetProperty("interviewed").GetInt32()); // a2 declinou depois da entrevista e continua contando
        Assert.Equal(10, m.GetProperty("avgCloseTimeDays").GetInt32());
        Assert.Equal(100, m.GetProperty("acceptance").GetProperty("rate").GetInt32());
        Assert.Equal(33, m.GetProperty("decline").GetProperty("rate").GetInt32());
        var funnel = m.GetProperty("funnel").EnumerateArray().Select(f => f.GetProperty("count").GetInt32()).ToList();
        Assert.Equal([3, 2, 1, 1, 1, 1], funnel);
        var hunting = m.GetProperty("sources").EnumerateArray().Single(s => s.Str("source") == "Hunting");
        Assert.Equal(2, hunting.GetProperty("total").GetInt32());
        Assert.Equal(50, hunting.GetProperty("pct").GetInt32());
        Assert.Equal(1, m.GetProperty("offerVsRequest").GetProperty("below").GetInt32());
        Assert.Contains(m.GetProperty("rejectionReasons").EnumerateArray(),
            r => r.Str("reason") == "Vaga preenchida por outro candidato" && r.GetProperty("count").GetInt32() == 1);

        var byCompany = await c.GetJson($"/api/metrics?companyId={company.Id()}");
        Assert.Equal(4, byCompany.GetProperty("totalAccessed").GetInt32());
        Assert.Equal(1, byCompany.GetProperty("openJobs").GetInt32());
    }

    [Fact]
    public async Task Exportacao_respeita_filtro_e_so_admin_recebe_colunas_sensiveis()
    {
        var c = await factory.LoginAsync();
        var tag = Seed.Unique("Exporta");
        await Seed.Candidate(c, new { name = tag + " A" });
        await Seed.Candidate(c, new { name = tag + " B" });
        await Seed.Candidate(c, new { name = Seed.Unique("Fora do filtro") });

        async Task<IXLWorksheet> Sheet(string url)
        {
            var res = await c.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", res.Content.Headers.ContentType?.MediaType);
            var wb = new XLWorkbook(await res.Content.ReadAsStreamAsync());
            return wb.Worksheet(1);
        }

        var plain = await Sheet($"/api/exports/candidates?search={Uri.EscapeDataString(tag)}");
        Assert.Equal(3, plain.LastRowUsed()!.RowNumber()); // cabeçalho + 2
        var headers = plain.Row(1).CellsUsed().Select(x => x.GetString()).ToList();
        Assert.DoesNotContain("Diversidade", headers);
        Assert.DoesNotContain("Remuneração atual", headers);

        var sensitive = await Sheet($"/api/exports/candidates?search={Uri.EscapeDataString(tag)}&includeSensitive=true");
        Assert.Contains("Diversidade", sensitive.Row(1).CellsUsed().Select(x => x.GetString()));

        var job = await Seed.Job(c, (await Seed.Company(c)).Id());
        await Seed.Application(c, (await Seed.Candidate(c)).Id(), job.Id());
        var res = await c.GetAsync($"/api/exports/pipeline?jobId={job.Id()}");
        var wbPipe = new XLWorkbook(await res.Content.ReadAsStreamAsync());
        Assert.Equal(["Prospecções", "Vagas"], wbPipe.Worksheets.Select(w => w.Name));
        Assert.Equal(2, wbPipe.Worksheet("Prospecções").LastRowUsed()!.RowNumber());
    }

    private static object Row(int n, string? name, string? linkedIn = null, string? phone = null, string? company = null,
        string? source = null, decimal? salary = null, decimal? requested = null, string? notes = null) =>
        new { rowNumber = n, name, linkedIn, phone, currentCompany = company, currentPosition = (string?)null, source, currentSalary = salary, requestedSalary = requested, notes };

    [Fact]
    public async Task Importacao_identifica_novos_iguais_conflitos_duplicados_e_linhas_invalidas()
    {
        var c = await factory.LoginAsync();
        var job = await Seed.Job(c, (await Seed.Company(c)).Id());
        var li = "imp-" + Guid.NewGuid().ToString("N")[..8];
        var existing = await Seed.Candidate(c, new { name = "Pessoa Existente", linkedIn = li, currentCompany = "Empresa Velha", salary = 8000 });
        var same = await Seed.Candidate(c, new { name = Seed.Unique("Igual"), phone = "51 98888-1111", currentCompany = "Mesma" });

        var rows = new[]
        {
            Row(6, "Pessoa Nova " + li, $"linkedin.com/in/{li}-novo", "inmail"),
            Row(7, "Pessoa Existente", $"https://www.linkedin.com/in/{li}/", "inmail", "Empresa Nova", salary: 9000),
            Row(8, same.Str("name").ToUpper(), null, "(51) 98888-1111", "mesma"),
            Row(9, "Repetida " + li, $"linkedin.com/in/{li}-novo"),
            Row(10, null, null, "inmail"),
        };
        var preview = await c.PostJson("/api/imports/preview", new { jobIds = new[] { job.Id() }, rows });
        var flags = preview.GetProperty("rows").EnumerateArray().Select(r => r.Str("match")).ToList();
        Assert.Equal(["New", "Conflict", "ExactMatch", "DuplicateInFile", "Invalid"], flags);

        var conflict = preview.GetProperty("rows")[1];
        Assert.Equal(existing.Id(), conflict.GetProperty("existingCandidateId").GetGuid());
        var fields = conflict.GetProperty("differences").EnumerateArray().Select(d => d.Str("field")).ToList();
        Assert.Equal(["currentCompany", "salary"], fields);
        Assert.Equal(6, preview.GetProperty("rows")[3].GetProperty("duplicateOfRow").GetInt32());
    }

    [Fact]
    public async Task Importacao_para_em_conflito_sem_decisao_e_aplica_a_decisao_do_usuario()
    {
        var c = await factory.LoginAsync();
        var job = await Seed.Job(c, (await Seed.Company(c)).Id());
        var li1 = "keep-" + Guid.NewGuid().ToString("N")[..8];
        var li2 = "upd-" + Guid.NewGuid().ToString("N")[..8];
        var keep = await Seed.Candidate(c, new { name = "Manter", linkedIn = li1, currentCompany = "Banco", salary = 8000 });
        var upd = await Seed.Candidate(c, new { name = "Atualizar", linkedIn = li2, currentCompany = "Banco", salary = 8000 });

        object Confirm(object row, string? resolution, string? stage = null, string? status = null, bool include = true) =>
            new { row, include, resolution, jobId = job.Id(), stage, status, offLimitsOverride = false };

        var rowKeep = Row(1, "Manter", li1, null, "Planilha", "hunting", 9000, 12000, "Entrevista com a área.");
        var rowUpd = Row(2, "Atualizar", li2, null, "Planilha", "Indicações", 9000);
        var rowNew = Row(3, Seed.Unique("Nova"), null, "51 97777-0000", "X", "Gupy");
        var rowSkip = Row(4, Seed.Unique("Ignorada"));

        // Sem decisão nos conflitos → para antes de gravar.
        var stop = await c.PostJson("/api/imports/confirm",
            new { rows = new[] { Confirm(rowKeep, null), Confirm(rowUpd, null) } }, HttpStatusCode.Conflict);
        Assert.Equal("IMPORT_CONFLICTS", stop.Str("code"));
        Assert.Equal(0, (await c.GetJson($"/api/pipeline?jobId={job.Id()}")).GetArrayLength());

        var result = await c.PostJson("/api/imports/confirm", new
        {
            rows = new[]
            {
                Confirm(rowKeep, "KeepExisting"),
                Confirm(rowUpd, "Update"),
                Confirm(rowNew, null, "Entrevista", "Rejected"),
                Confirm(rowSkip, null, include: false),
            },
        });
        Assert.Equal(1, result.GetProperty("candidatesCreated").GetInt32());
        Assert.Equal(1, result.GetProperty("candidatesUpdated").GetInt32());
        Assert.Equal(1, result.GetProperty("candidatesKept").GetInt32());
        Assert.Equal(3, result.GetProperty("applicationsCreated").GetInt32());
        Assert.Contains(result.GetProperty("rows").EnumerateArray(), r => r.GetProperty("rowNumber").GetInt32() == 4 && r.Str("outcome") == "Skipped");

        Assert.Equal("Banco", (await c.GetJson($"/api/candidates/{keep.Id()}")).Str("currentCompany"));
        var updated = await c.GetJson($"/api/candidates/{upd.Id()}");
        Assert.Equal("Planilha", updated.Str("currentCompany"));
        Assert.Equal(9000m, updated.GetProperty("salary").GetDecimal());

        // Status inicial definido na importação (D3): Rejected não aparece no Kanban.
        var board = await c.GetJson($"/api/pipeline?jobId={job.Id()}");
        Assert.Equal(2, board.GetArrayLength());

        // Observações e expectativa ficam na prospecção (D4); fonte normalizada.
        var keepApp = (await c.GetJson($"/api/candidates/{keep.Id()}")).GetProperty("applications")[0];
        var detail = await c.GetJson($"/api/applications/{keepApp.Id()}");
        Assert.Equal("Entrevista com a área.", detail.Str("notes"));
        Assert.Equal(12000m, detail.GetProperty("requestedSalary").GetDecimal());
        Assert.Equal("Hunting", detail.Str("source"));
        var updApp = (await c.GetJson($"/api/candidates/{upd.Id()}")).GetProperty("applications")[0];
        Assert.Equal("Indicação", (await c.GetJson($"/api/applications/{updApp.Id()}")).Str("source"));

        // Reimportar a mesma linha não duplica: informa que já está na vaga.
        var again = await c.PostJson("/api/imports/confirm", new { rows = new[] { Confirm(rowKeep, "KeepExisting") } });
        Assert.Equal(0, again.GetProperty("applicationsCreated").GetInt32());
        Assert.Contains("já está na vaga", again.GetProperty("rows")[0].Str("reason"));
    }

    [Fact]
    public async Task Importacao_respeita_off_limits_e_duplicados_no_arquivo_viram_um_candidato()
    {
        var c = await factory.LoginAsync();
        var company = await Seed.Company(c);
        var jobA = await Seed.Job(c, company.Id(), "Analista Pleno");
        var jobB = await Seed.Job(c, company.Id(), "Analista Sênior");
        var li = "off-" + Guid.NewGuid().ToString("N")[..8];
        await Seed.Candidate(c, new { name = "Bloqueada", linkedIn = li, offLimitCompanyIds = new[] { company.Id() } });
        var dupLi = "dup-" + Guid.NewGuid().ToString("N")[..8];

        var result = await c.PostJson("/api/imports/confirm", new
        {
            rows = new object[]
            {
                new { row = Row(1, "Bloqueada", li), include = true, resolution = (string?)null, jobId = jobA.Id(), stage = (string?)null, status = (string?)null, offLimitsOverride = false },
                new { row = Row(2, "Dupla", dupLi), include = true, resolution = (string?)null, jobId = jobA.Id(), stage = (string?)null, status = (string?)null, offLimitsOverride = false },
                new { row = Row(3, "Dupla", dupLi), include = true, resolution = (string?)null, jobId = jobB.Id(), stage = (string?)null, status = (string?)null, offLimitsOverride = false },
            },
        });
        Assert.Contains("Off-limits", result.GetProperty("rows")[0].Str("reason"));
        Assert.Equal(1, result.GetProperty("candidatesCreated").GetInt32());
        Assert.Equal(2, result.GetProperty("applicationsCreated").GetInt32());
    }
}
