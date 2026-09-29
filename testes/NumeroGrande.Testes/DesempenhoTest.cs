using System.Numerics;
using Xunit;

namespace Conde.NumeroGrande.Testes;

/// <summary>
/// Os números grandes de verdade — e a lição sobre onde medir tempo.
/// </summary>
/// <remarks>
/// <para>
/// Esta classe começou com quatro testes de tempo e ficou com nenhum. A
/// história vale mais que os testes que ela tinha.
/// </para>
/// <para>
/// O primeiro a cair comparava o limite de Karatsuba medido (128) com o que eu
/// havia copiado do OpenJDK (32). Ele falhava de forma intermitente <b>na minha
/// máquina</b>: sob o executor de testes, com várias classes em paralelo, as
/// duas medidas davam 10,0 ms e 9,8 ms — uma diferença de ruído. Na ferramenta,
/// a mesma comparação dá 1,3 ms contra 1,7 ms, estável.
/// </para>
/// <para>
/// Os outros três caíram no <b>CI</b>, e nos três sistemas ao mesmo tempo. Um
/// deles afirmava que Karatsuba ganha da conta de escola em quatro mil palavras
/// — o que é verdade na minha máquina, com repetições suficientes, e não é
/// verificável numa máquina compartilhada com cinco repetições.
/// </para>
/// <para>
/// A conclusão não é que as medidas estavam erradas: é que <b>o lugar delas não
/// é aqui</b>. Um teste que falha sem que nada tenha piorado ensina a ignorar
/// testes, e essa é a pior coisa que pode acontecer com uma bateria. As medidas
/// vivem na ferramenta, que roda no CI para registrar os números de cada
/// máquina, e nunca falha por causa deles.
/// </para>
/// <para>
/// O que ficou aqui são afirmações de <b>correção</b> em números grandes, que
/// não dependem de relógio nenhum.
/// </para>
/// </remarks>
public class DesempenhoTest
{
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

    [Fact(DisplayName = "os dois algoritmos concordam em números de cem mil bits")]
    public void OsDoisAlgoritmosConcordamEmNumerosEnormes()
    {
        // Não é sobre tempo: é sobre a recursão de Karatsuba estar certa em
        // todos os níveis. Com quatro mil palavras e limite de 128, são cinco
        // níveis de recursão, e um erro de fronteira em qualquer um deles muda
        // o resultado.
        var sorteio = new Random(20260929);

        for (var i = 0; i < 20; i++)
        {
            var a = Sortear(sorteio, 4_096);
            var b = Sortear(sorteio, 4_096);

            var antes = Magnitude.LimiteDeKaratsuba;

            try
            {
                Magnitude.LimiteDeKaratsuba = int.MaxValue;

                var deEscola = Magnitude.MultiplicarDeEscola(a, b);

                Magnitude.LimiteDeKaratsuba = 128;

                var deKaratsuba = Magnitude.Multiplicar(a, b);

                Assert.Equal(deEscola, deKaratsuba);
            }
            finally
            {
                Magnitude.LimiteDeKaratsuba = antes;
            }
        }
    }

    [Fact(DisplayName = "qualquer limite de Karatsuba dá o mesmo resultado")]
    public void QualquerLimiteDaOMesmo()
    {
        // O limite é uma escolha de desempenho e não pode mudar o resultado.
        // Se mudar, a recursão tem um caso de fronteira errado -- e o limite é
        // justamente o que decide quando ela acontece.
        var sorteio = new Random(7);

        var a = Sortear(sorteio, 700);
        var b = Sortear(sorteio, 500);

        var antes = Magnitude.LimiteDeKaratsuba;

        try
        {
            var esperado = Magnitude.MultiplicarDeEscola(a, b);

            foreach (var limite in (int[])[2, 3, 7, 16, 32, 64, 128, 256, 512, 1024])
            {
                Magnitude.LimiteDeKaratsuba = limite;

                Assert.Equal(esperado, Magnitude.Multiplicar(a, b));
            }
        }
        finally
        {
            Magnitude.LimiteDeKaratsuba = antes;
        }
    }

    [Fact(DisplayName = "a potência modular de mil bits dá o mesmo que a do .NET")]
    public void PotenciaModularDeMilBits()
    {
        // É a conta que segura o RSA. Com o resto tirado a cada passo, nenhum
        // intermediário passa do dobro do módulo; sem ele, os números
        // intermediários teriam milhões de dígitos e a conta ficaria impossível.
        var sorteio = new Random(13);

        string Trezentos() => new(Enumerable.Range(0, 300)
            .Select(_ => (char)('1' + sorteio.Next(9))).ToArray());

        for (var i = 0; i < 5; i++)
        {
            var baseA = Trezentos();
            var expoente = Trezentos();
            var modulo = Trezentos();

            Assert.Equal(
                BigInteger.ModPow(
                    BigInteger.Parse(baseA),
                    BigInteger.Parse(expoente),
                    BigInteger.Parse(modulo)).ToString(),
                Numero.Ler(baseA).ElevarModulo(Numero.Ler(expoente), Numero.Ler(modulo))
                    .ToString());
        }
    }

    [Fact(DisplayName = "um número de cem mil dígitos atravessa tudo sem perder nada")]
    public void CemMilDigitos()
    {
        var sorteio = new Random(17);

        var texto = new string(Enumerable.Range(0, 100_000)
            .Select(i => (char)(i == 0 ? '1' + sorteio.Next(9) : '0' + sorteio.Next(10)))
            .ToArray());

        var meu = Numero.Ler(texto);
        var dele = BigInteger.Parse(texto);

        Assert.Equal(texto, meu.ToString());
        Assert.Equal(dele.ToString(), meu.ToString());

        // E a ida e volta pela base 16, que é o caminho barato.
        Assert.Equal(meu.ToString(), Numero.Ler(meu.Escrever(16), 16).ToString());
    }
}
