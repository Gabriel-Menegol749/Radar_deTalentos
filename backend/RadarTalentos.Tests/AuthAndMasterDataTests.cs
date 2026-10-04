using System.Net;
using System.Net.Http.Json;

namespace RadarTalentos.Tests;

[Collection("api")]
public class AuthAndMasterDataTests(ApiFactory factory)
{
    [Fact]
    public async Task Login_invalido_e_rota_sem_token_sao_recusados()
    {
        var anon = factory.CreateClient();
        var bad = await anon.PostAsJsonAsync("/api/auth/login", new { email = ApiFactory.AdminEmail, password = "errada" });
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/companies")).StatusCode);
    }

    [Fact]
    public async Task Consultor_nao_gerencia_usuarios_nem_exporta_dados_sensiveis()
    {
        var admin = await factory.LoginAsync();
        var email = $"consultor{Guid.NewGuid():N}@teste.local";
        await admin.PostJson("/api/users", new { name = "Consultora", email, password = "Senha@123", role = "Consultor" });

        var consultor = await factory.LoginAsync(email, "Senha@123");
        Assert.Equal(HttpStatusCode.Forbidden, (await consultor.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await consultor.GetAsync("/api/exports/candidates?includeSensitive=true")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await consultor.GetAsync("/api/exports/candidates")).StatusCode);
        // Consultor opera o dia a dia.
        await Seed.Company(consultor);
    }

    [Fact]
    public async Task Criar_usuario_valida_email_e_recusa_duplicado_com_409()
    {
        var admin = await factory.LoginAsync();
        await admin.PostJson("/api/users", new { name = "X", email = "sem-arroba.com", password = "Senha@123", role = "Consultor" }, HttpStatusCode.BadRequest);
        var email = $"dup{Guid.NewGuid():N}@teste.local";
        await admin.PostJson("/api/users", new { name = "A", email, password = "Senha@123", role = "Consultor" });
        var dup = await admin.PostJson("/api/users", new { name = "B", email = email.ToUpper(), password = "Senha@123", role = "Consultor" }, HttpStatusCode.Conflict);
        Assert.Contains("e-mail", dup.Str("message"));
    }

    [Fact]
    public async Task Usuario_inativado_nao_consegue_logar()
    {
        var admin = await factory.LoginAsync();
        var email = $"inativo{Guid.NewGuid():N}@teste.local";
        var user = await admin.PostJson("/api/users", new { name = "Inativo", email, password = "Senha@123", role = "Consultor" });
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/users/{user.Id()}")).StatusCode);
        var res = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password = "Senha@123" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Codigo_da_vaga_segue_formato_do_prototipo_e_sequencia_por_posicao()
    {
        var c = await factory.LoginAsync();
        var company = await Seed.Company(c, "Sicredi Teste " + Guid.NewGuid().ToString("N")[..4]);
        var preview = await c.GetJson($"/api/jobs/code-preview?companyId={company.Id()}&positionName=Atração e Seleção&openedAt=2026-07-01");
        var job1 = await Seed.Job(c, company.Id(), "Atração e Seleção", "2026-07-01");
        Assert.Equal(preview.Str("code"), job1.Str("code"));
        Assert.Matches(@"^SICREDITESTE-ATRACAOESELECA-260701-01$", job1.Str("code"));

        // Mesma posição (sem diferenciar maiúsculas) → reaproveita a posição e incrementa NN.
        var job2 = await Seed.Job(c, company.Id(), "ATRAÇÃO E SELEÇÃO", "2026-07-01");
        Assert.EndsWith("-260701-02", job2.Str("code"));
        Assert.Equal(job1.Str("positionId"), job2.Str("positionId"));
    }

    [Fact]
    public async Task Reabrir_vaga_cria_nova_abertura_com_novo_codigo()
    {
        var c = await factory.LoginAsync();
        var company = await Seed.Company(c);
        var job = await Seed.Job(c, company.Id());
        await c.PostJson($"/api/jobs/{job.Id()}/reopen", new { }, HttpStatusCode.BadRequest); // aberta não reabre
        await c.PostJson($"/api/jobs/{job.Id()}/close", new { });
        var reopened = await c.PostJson($"/api/jobs/{job.Id()}/reopen", new { });
        Assert.NotEqual(job.Str("code"), reopened.Str("code"));
        Assert.Equal(job.Str("code"), reopened.Str("reopenedFromCode"));
        Assert.Equal("Open", reopened.Str("status"));
    }

    [Fact]
    public async Task Empresa_inativada_some_da_lista_e_pode_ser_reativada()
    {
        var c = await factory.LoginAsync();
        var name = Seed.Unique("Empresa Soft");
        var company = await Seed.Company(c, name);
        await c.PostJson("/api/companies", new { name = name.ToUpper() }, HttpStatusCode.Conflict);
        await c.DeleteAsync($"/api/companies/{company.Id()}");
        var list = await c.GetJson("/api/companies");
        Assert.DoesNotContain(list.EnumerateArray(), e => e.Str("name") == name);
        var again = await Seed.Company(c, name);
        Assert.Equal(company.Id(), again.Id());
    }
}
