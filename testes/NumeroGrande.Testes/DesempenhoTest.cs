using System.Diagnostics;
using System.Numerics;
using Xunit;

namespace Conde.NumeroGrande.Testes;

/// <summary>
/// As promessas de custo, medidas em vez de afirmadas.
/// </summary>
/// <remarks>
/// <para>
/// Um teste de tempo é frágil por natureza: a máquina do CI é compartilhada, o
/// coletor de lixo entra quando quer, e um número apertado demais transforma
/// uma variação normal em falha. Por isso as margens aqui são largas — elas não
/// medem velocidade, medem <b>o formato da curva</b>.
/// </para>
/// <para>
/// O que se quer garantir é que uma escolha medida continue valendo. O limite de
/// Karatsuba foi escolhido com a ferramenta, e sem um teste ele volta a ser um
/// número no código que ninguém sabe de onde veio.
/// </para>
/// <para>
/// E há o que <b>não</b> está aqui. A primeira versão tinha um teste comparando
/// o limite medido (128) com o que eu havia copiado do OpenJDK (32) — e ele
/// falhava de forma intermitente: sob o executor de testes, com várias classes
/// em paralelo, as duas medidas davam 10 ms e 9,8 ms, uma diferença de ruído.
/// A mesma comparação, na ferramenta, dá 1,3 ms contra 1,7 ms de forma estável.
/// </para>
/// <para>
/// A conclusão não é que a medida estava errada: é que <b>o lugar dela não é
/// aqui</b>. Um teste que falha sem que nada tenha piorado ensina a ignorar
/// testes, que é o pior que pode acontecer com uma bateria.
/// </para>
/// </remarks>
[Collection("medidas de tempo")]
public class DesempenhoTest
{
    private static double Cronometrar(int vezes, Action acao)
    {
        acao();

        var relogio = Stopwatch.StartNew();

        for (var i = 0; i < vezes; i++)
        {
            acao();
        }

        return relogio.Elapsed.TotalMilliseconds / vezes;
    }

    private static uint[] Sortear(Random sorteio, int palavras)
    {
        var numero = new uint[palavras];

        for (var i = 0; i < palavras; i++)
        {
            numero[i] = (uint)sorteio.Next(int.MinValue, int.MaxValue);
        }

        numero[^1] |= 0x8000_0000;

        return numero;
    }

    [Fact(DisplayName = "Karatsuba ganha da conta de escola em números grandes")]
    public void KaratsubaGanha()
    {
        // É a razão de o algoritmo estar no código. Se ele parar de ganhar --
        // por uma mudança no limite, na alocação ou na recursão --, o número no
        // código vira decoração e este teste avisa.
        var sorteio = new Random(20260929);

        var a = Sortear(sorteio, 4096);
        var b = Sortear(sorteio, 4096);

        // Quatro mil palavras são 131 mil bits, onde a vantagem assintótica é
        // grande o bastante para o ruído do executor não a apagar.
        var deEscola = Cronometrar(5, () => Magnitude.MultiplicarDeEscola(a, b));
        var deKaratsuba = Cronometrar(5, () => Magnitude.Multiplicar(a, b));

        Assert.True(deKaratsuba < deEscola,
            $"Karatsuba levou {deKaratsuba:F3} ms e a conta de escola {deEscola:F3} ms "
            + $"(limite: {Magnitude.LimiteDeKaratsuba} palavras)");
    }

    [Fact(DisplayName = "dobrar o tamanho não quadruplica o tempo")]
    public void OCustoNaoEhQuadratico()
    {
        // A promessa de Karatsuba é n^1,585 em vez de n². Dobrar o tamanho
        // multiplica o tempo por 2^1,585 ≈ 3, e não por 4. A margem é folgada
        // porque um teste de tempo no CI não mede constante -- mede formato.
        var sorteio = new Random(11);

        var a = Sortear(sorteio, 2048);
        var b = Sortear(sorteio, 2048);

        var dobroA = Sortear(sorteio, 4096);
        var dobroB = Sortear(sorteio, 4096);

        var menor = Cronometrar(5, () => Magnitude.Multiplicar(a, b));
        var maior = Cronometrar(5, () => Magnitude.Multiplicar(dobroA, dobroB));

        var razao = maior / Math.Max(0.0001, menor);

        // A margem é folgada de propósito: 2^1,585 é 3, e o teto de 4 dá espaço
        // para o ruído sem deixar passar um custo que voltou a ser quadrático.
        Assert.True(razao < 4.0,
            $"dobrar o tamanho multiplicou o tempo por {razao:F2}: "
            + $"{menor:F3} ms contra {maior:F3} ms");
    }

    [Fact(DisplayName = "não fica absurdamente longe do BigInteger do .NET")]
    public void PertoDoDotnet()
    {
        // Não é para ganhar: o BigInteger do .NET tem anos de otimização e
        // caminhos em código nativo. É para saber a distância -- e uma razão de
        // duas ou três vezes quer dizer que o algoritmo está certo e falta o
        // acabamento. Vinte vezes quereria dizer outra coisa.
        var texto = string.Concat(Enumerable.Range(0, 10_000).Select(i => (char)('0' + i % 9 + 1)));

        var meuA = Numero.Ler(texto);
        var meuB = Numero.Ler(texto[1..] + "7");

        var deleA = BigInteger.Parse(texto);
        var deleB = BigInteger.Parse(texto[1..] + "7");

        var meu = Cronometrar(20, () => _ = meuA * meuB);
        var dele = Cronometrar(20, () => _ = deleA * deleB);

        Assert.True(meu < dele * 15,
            $"meu: {meu:F3} ms, .NET: {dele:F3} ms — {meu / dele:F1} vezes mais lento");
    }

    [Fact(DisplayName = "a potência modular de mil bits sai num piscar")]
    public void PotenciaModularEhViavel()
    {
        // É a conta que segura o RSA. Com o resto tirado a cada passo, nenhum
        // intermediário passa do dobro do módulo; sem ele, 2^1024 seria um
        // número de trezentos dígitos e a conta ficaria impossível.
        var sorteio = new Random(13);

        var baseA = Numero.Ler(new string(Enumerable.Range(0, 300)
            .Select(_ => (char)('1' + sorteio.Next(9))).ToArray()));

        var expoente = Numero.Ler(new string(Enumerable.Range(0, 300)
            .Select(_ => (char)('1' + sorteio.Next(9))).ToArray()));

        var modulo = Numero.Ler(new string(Enumerable.Range(0, 300)
            .Select(_ => (char)('1' + sorteio.Next(9))).ToArray()));

        var gasto = Cronometrar(1, () => _ = baseA.ElevarModulo(expoente, modulo));

        Assert.True(gasto < 5_000,
            $"a potência modular levou {gasto:F0} ms");

        Assert.Equal(
            BigInteger.ModPow(
                BigInteger.Parse(baseA.ToString()),
                BigInteger.Parse(expoente.ToString()),
                BigInteger.Parse(modulo.ToString())).ToString(),
            baseA.ElevarModulo(expoente, modulo).ToString());
    }
}
