using System.Net;
using System.Text.Json;

namespace RadarTalentos.Tests;

[Collection("api")]
public class CandidatePipelineTests(ApiFactory factory)
{
    [Fact]
    public async Task Listagem_omite_diversidade_e_remuneracao_mas_detalhe_mostra()
    {
        var c = await factory.LoginAsync();
        var cand = await Seed.Candidate(c, new { name = Seed.Unique("Sigilo") });
        var list = await c.GetJson($"/api/candidates?search={Uri.EscapeDataString(cand.Str("name"))}");
        var item = list.GetProperty("items")[0];
        Assert.False(item.TryGetProperty("diversityTags", out _));
        Assert.False(item.TryGetProperty("salary", out _));
        var detail = await c.GetJson($"/api/candidates/{cand.Id()}");
        Assert.Equal("PCD", detail.GetProperty("diversityTags")[0].GetString());
        Assert.Equal(10000m, detail.GetProperty("salary").GetDecimal());
    }

    [Fact]
    public async Task LinkedIn_e_normalizado_e_duplicado_e_recusado()
    {
        var c = await factory.LoginAsync();
        var handle = "pessoa-" + Guid.NewGuid().ToString("N")[..8];
        var cand = await Seed.Candidate(c, new { linkedIn = $"https://www.linkedin.com/in/{handle}/pt/?lipi=urn%3Ali%3Apage" });
        Assert.Equal($"linkedin.com/in/{handle}", cand.Str("linkedIn"));
        await c.PostJson("/api/candidates", new { name = "Outra", linkedIn = handle.ToUpper() }, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Telefone_inmail_e_preservado_e_formato_padronizado()
    {
        var c = await factory.LoginAsync();
        var a = await Seed.Candidate(c, new { phone = "inmail " });
        Assert.Equal("inmail", a.Str("phone"));
        var b = await Seed.Candidate(c, new { phone = "51 9 9362-6318" });
        Assert.Equal("(51) 99362-6318", b.Str("phone"));
        Assert.Equal("51 9 9362-6318", b.Str("phoneOriginal"));
    }

    [Fact]
    public async Task Candidato_inativado_some_da_lista_e_preserva_historico()
    {
        var c = await factory.LoginAsync();
        var company = await Seed.Company(c);
        var job = await Seed.Job(c, company.Id());
        var cand = await Seed.Candidate(c);
        var app = await Seed.Application(c, cand.Id(), job.Id());
        await c.DeleteAsync($"/api/candidates/{cand.Id()}");
        var list = await c.GetJson($"/api/candidates?search={Uri.EscapeDataString(cand.Str("name"))}");
        Assert.Equal(0, list.GetProperty("total").GetInt32());
        var detail = await c.GetJson($"/api/applications/{app.Id()}");
        Assert.Equal(cand.Str("name"), detail.GetProperty("candidate").Str("name"));
    }

    [Fact]
    public async Task Off_limits_bloqueia_por_padrao_e_override_fica_registrado()
    {
        var c = await factory.LoginAsync();
        var company = await Seed.Company(c);
        var job = await Seed.Job(c, company.Id());
        var cand = await Seed.Candidate(c, new { offLimitCompanyIds = new[] { company.Id() } });

        var blocked = await c.PostJson("/api/applications", new { candidateId = cand.Id(), jobId = job.Id() }, HttpStatusCode.Conflict);
        Assert.Equal("OFF_LIMITS", blocked.Str("code"));

        var app = await c.PostJson("/api/applications", new { candidateId = cand.Id(), jobId = job.Id(), offLimitsOverride = true });
        Assert.True(app.GetProperty("offLimitsOverride").GetBoolean());
        Assert.Equal("Administrador", app.Str("offLimitsOverrideBy"));
        Assert.False(app.GetProperty("offLimitsOverrideAt").ValueKind == JsonValueKind.Null);

        // Candidato sem off-limits não é afetado pela flag.
        var free = await Seed.Candidate(c);
        var freeApp = await c.PostJson("/api/applications", new { candidateId = free.Id(), jobId = job.Id(), offLimitsOverride = true });
        Assert.False(freeApp.GetProperty("offLimitsOverride").GetBoolean());
    }

    [Fact]
    public async Task Mesma_pessoa_nao_entra_duas_vezes_na_mesma_vaga()
    {
        var c = await factory.LoginAsync();
        var job = await Seed.Job(c, (await Seed.Company(c)).Id());
        var cand = await Seed.Candidate(c);
        await Seed.Application(c, cand.Id(), job.Id());
        await c.PostJson("/api/applications", new { candidateId = cand.Id(), jobId = job.Id() }, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Mover_etapa_grava_historico_e_fecha_registro_anterior()
    {
        var c = await factory.LoginAsync();
        var job = await Seed.Job(c, (await Seed.Company(c)).Id());
        var app = await Seed.Application(c, (await Seed.Candidate(c)).Id(), job.Id());

        await c.PostJson($"/api/applications/{app.Id()}/move", new { targetStage = "Entrevista", notes = "Entrevista 29/07" });
        var moved = await c.PostJson($"/api/applications/{app.Id()}/move", new { targetStage = "Aprovação", offeredSalary = 9000, requestedSalary = 10000 });
        var back = await c.PostJson($"/api/applications/{app.Id()}/move", new { targetStage = "Entrevista" });

        var history = back.GetProperty("history").EnumerateArray().ToList();
        Assert.Equal(["Acessado", "Entrevista", "Aprovação", "Entrevista"], history.Select(h => h.Str("stage")));
        Assert.All(history.Take(3), h => Assert.NotEqual(JsonValueKind.Null, h.GetProperty("exitedAt").ValueKind));
        Assert.Equal(JsonValueKind.Null, history[3].GetProperty("exitedAt").ValueKind);
        Assert.Equal("Retorno manual", history[3].Str("notes"));
        Assert.Equal("Entrevista 29/07", history[1].Str("notes"));
        Assert.Equal(9000m, moved.GetProperty("offeredSalary").GetDecimal());

        await c.PostJson($"/api/applications/{app.Id()}/move", new { targetStage = "Inexistente" }, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Nao_aderente_vira_Rejected_com_varios_motivos_e_sai_do_kanban()
    {
        var c = await factory.LoginAsync();
        var job = await Seed.Job(c, (await Seed.Company(c)).Id());
        var app = await Seed.Application(c, (await Seed.Candidate(c)).Id(), job.Id());

        var resolved = await c.PostJson($"/api/applications/{app.Id()}/resolve",
            new { status = "Rejected", reasons = new[] { "Senioridade", "Perfil comportamental" } });
        Assert.Equal("Rejected", resolved.Str("status"));
        Assert.Equal(2, resolved.GetProperty("rejectionReasons").GetArrayLength());

        var board = await c.GetJson($"/api/pipeline?jobId={job.Id()}");
        Assert.Equal(0, board.GetArrayLength());
        await c.PostJson($"/api/applications/{app.Id()}/move", new { targetStage = "Entrevista" }, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Contratacao_fecha_vaga_e_encerra_demais_ativas_como_sem_sucesso()
    {
        var c = await factory.LoginAsync();
        var job = await Seed.Job(c, (await Seed.Company(c)).Id());
        var hired = await Seed.Application(c, (await Seed.Candidate(c)).Id(), job.Id());
        var other = await Seed.Application(c, (await Seed.Candidate(c)).Id(), job.Id());
        var declined = await Seed.Application(c, (await Seed.Candidate(c)).Id(), job.Id());
        await c.PostJson($"/api/applications/{declined.Id()}/resolve", new { status = "Declined" });

        await c.PostJson($"/api/applications/{hired.Id()}/resolve", new { status = "Hired" });

        var jobAfter = await c.GetJson($"/api/jobs/{job.Id()}");
        Assert.Equal("Closed", jobAfter.Str("status"));
        Assert.NotEqual(JsonValueKind.Null, jobAfter.GetProperty("closedAt").ValueKind);

        var otherAfter = await c.GetJson($"/api/applications/{other.Id()}");
        Assert.Equal("Rejected", otherAfter.Str("status"));
        Assert.Equal("Vaga preenchida por outro candidato", otherAfter.GetProperty("rejectionReasons")[0].GetString());
        Assert.Equal("Declined", (await c.GetJson($"/api/applications/{declined.Id()}")).Str("status"));

        // Vaga fechada não recebe novas prospecções.
        await c.PostJson("/api/applications", new { candidateId = (await Seed.Candidate(c)).Id(), jobId = job.Id() }, HttpStatusCode.BadRequest);
    }
}
