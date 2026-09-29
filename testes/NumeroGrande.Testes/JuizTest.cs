using System.Numerics;
using Xunit;

namespace Conde.NumeroGrande.Testes;

/// <summary>
/// O juiz: o <c>System.Numerics.BigInteger</c> do próprio .NET.
/// </summary>
/// <remarks>
/// <para>
/// É o melhor juiz que um projeto destes pode ter, por duas razões que não se
/// encontram juntas com frequência.
/// </para>
/// <para>
/// <b>Ele é exato.</b> Não há tolerância, não há arredondamento, não há
/// "próximo o bastante": ou os dois números são o mesmo ou não são. Num projeto
/// de ponto flutuante a comparação já começa com uma discussão sobre epsilon;
/// aqui não há discussão.
/// </para>
/// <para>
/// <b>Ele é ilimitado.</b> Dá para gerar milhões de casos de qualquer tamanho e
/// comparar todos. Não há tabela de vetores para acabar, não há corpus para
/// baixar: o juiz responde a qualquer pergunta que se faça.
/// </para>
/// <para>
/// E o que se prova aqui é forte de verdade. Uma aritmética com defeito não
/// estoura: ela devolve um número. Um número errado num dígito do meio, que
/// passa por toda conferência que uma pessoa faria a olho.
/// </para>
/// </remarks>
public class JuizTest
{
    private static Numero Meu(BigInteger dele) => Numero.Ler(dele.ToString());

    private static BigInteger Dele(Numero meu) => BigInteger.Parse(meu.ToString());

    /// <summary>Um número sorteado, com tamanhos que cobrem os caminhos todos.</summary>
    private static BigInteger Sortear(Random sorteio, int maximoDeBits = 512)
    {
        var bits = sorteio.Next(3) switch
        {
            // Pequenos de propósito: é onde os casos de borda moram.
            0 => sorteio.Next(1, 65),
            1 => sorteio.Next(1, 200),
            _ => sorteio.Next(1, maximoDeBits),
        };

        var bytes = new byte[(bits + 7) / 8 + 1];

        sorteio.NextBytes(bytes);

        // O último byte zerado força o número a ser positivo: o BigInteger lê
        // em complemento de dois e o bit alto seria o sinal.
        bytes[^1] = 0;

        var valor = new BigInteger(bytes) >> (bytes.Length * 8 - 8 - bits);

        return sorteio.Next(2) == 0 ? valor : -valor;
    }

    [Fact(DisplayName = "cem mil somas e subtrações")]
    public void SomaESubtracao()
    {
        var sorteio = new Random(20260929);

        for (var i = 0; i < 100_000; i++)
        {
            var a = Sortear(sorteio);
            var b = Sortear(sorteio);

            Assert.Equal((a + b).ToString(), (Meu(a) + Meu(b)).ToString());
            Assert.Equal((a - b).ToString(), (Meu(a) - Meu(b)).ToString());
            Assert.Equal((-a).ToString(), (-Meu(a)).ToString());
        }
    }

    [Fact(DisplayName = "cem mil multiplicações, passando pelos dois algoritmos")]
    public void Multiplicacao()
    {
        // O sorteio cobre números de 1 a 512 bits, e o limite de Karatsuba é de
        // 32 palavras (1024 bits) -- então este teste exercita a conta de
        // escola. O de baixo força o outro caminho.
        var sorteio = new Random(7);

        for (var i = 0; i < 100_000; i++)
        {
            var a = Sortear(sorteio);
            var b = Sortear(sorteio);

            Assert.Equal((a * b).ToString(), (Meu(a) * Meu(b)).ToString());
        }
    }

    [Fact(DisplayName = "os dois algoritmos de multiplicação dão o mesmo, sempre")]
    public void OsDoisAlgoritmosConcordam()
    {
        // Comparar Karatsuba com a conta de escola é mais forte que comparar
        // cada um com o juiz: se um dia os dois caminhos divergirem, é aqui que
        // aparece, mesmo que ambos ainda batam com o .NET em números pequenos.
        var sorteio = new Random(11);

        var antes = Magnitude.LimiteDeKaratsuba;

        try
        {
            for (var i = 0; i < 3_000; i++)
            {
                var a = Sortear(sorteio, 4_000);
                var b = Sortear(sorteio, 4_000);

                Magnitude.LimiteDeKaratsuba = int.MaxValue;

                var deEscola = Meu(a) * Meu(b);

                Magnitude.LimiteDeKaratsuba = 2;

                var deKaratsuba = Meu(a) * Meu(b);

                Assert.Equal(deEscola.ToString(), deKaratsuba.ToString());
                Assert.Equal((a * b).ToString(), deKaratsuba.ToString());
            }
        }
        finally
        {
            Magnitude.LimiteDeKaratsuba = antes;
        }
    }

    [Fact(DisplayName = "cem mil divisões, com quociente e resto")]
    public void Divisao()
    {
        // A divisão é a única operação difícil de um número grande, e a
        // correção de Knuth que a torna correta acontece em menos de dois por
        // mil dos casos. Cem mil divisões dão umas duzentas chances de ela
        // estar errada e aparecer.
        var sorteio = new Random(13);

        for (var i = 0; i < 100_000; i++)
        {
            var a = Sortear(sorteio);
            var b = Sortear(sorteio);

            if (b.IsZero)
            {
                continue;
            }

            var (quociente, resto) = Numero.Dividir(Meu(a), Meu(b));

            Assert.Equal(BigInteger.Divide(a, b).ToString(), quociente.ToString());
            Assert.Equal(BigInteger.Remainder(a, b).ToString(), resto.ToString());

            // E a identidade que define a divisão: a = q*b + r, sempre.
            Assert.Equal(a.ToString(), (quociente * Meu(b) + resto).ToString());
        }
    }

    [Fact(DisplayName = "a divisão com divisor grande, que é onde o algoritmo D mora")]
    public void DivisaoComDivisorGrande()
    {
        // Divisor de uma palavra tem caminho próprio, e é o comum. Este teste
        // força o outro: divisores de dezenas de palavras, que é onde o palpite
        // de Knuth e as duas correções trabalham.
        var sorteio = new Random(17);

        for (var i = 0; i < 20_000; i++)
        {
            var b = Sortear(sorteio, 2_000);

            if (b.IsZero || BigInteger.Abs(b) < BigInteger.Pow(2, 64))
            {
                continue;
            }

            var a = b * Sortear(sorteio, 1_000) + Sortear(sorteio, 500);

            var (quociente, resto) = Numero.Dividir(Meu(a), Meu(b));

            Assert.Equal(BigInteger.Divide(a, b).ToString(), quociente.ToString());
            Assert.Equal(BigInteger.Remainder(a, b).ToString(), resto.ToString());
        }
    }

    [Fact(DisplayName = "o sinal do resto é o do dividendo, como em C e em C#")]
    public void OSinalDoResto()
    {
        // `-7 % 2` é `-1` aqui e em C, e é `1` em Python. As duas convenções
        // são razoáveis, e ser diferente do juiz por gosto próprio só cria
        // armadilha para quem usa os dois.
        (long A, long B)[] casos =
        [
            (7, 2), (-7, 2), (7, -2), (-7, -2),
            (1, 3), (-1, 3), (0, 5), (100, 7), (-100, 7),
        ];

        foreach (var (a, b) in casos)
        {
            var (quociente, resto) = Numero.Dividir(Numero.De(a), Numero.De(b));

            Assert.Equal((a / b).ToString(), quociente.ToString());
            Assert.Equal((a % b).ToString(), resto.ToString());
        }
    }

    [Fact(DisplayName = "comparação e ordenação")]
    public void Comparacao()
    {
        var sorteio = new Random(19);

        for (var i = 0; i < 50_000; i++)
        {
            var a = Sortear(sorteio);
            var b = Sortear(sorteio);

            Assert.Equal(Math.Sign(a.CompareTo(b)), Math.Sign(Meu(a).CompareTo(Meu(b))));
            Assert.Equal(a == b, Meu(a) == Meu(b));
            Assert.Equal(a < b, Meu(a) < Meu(b));
            Assert.Equal(a > b, Meu(a) > Meu(b));
        }
    }

    [Fact(DisplayName = "potência, deslocamento e mdc")]
    public void OutrasContas()
    {
        var sorteio = new Random(23);

        for (var i = 0; i < 3_000; i++)
        {
            var a = Sortear(sorteio, 128);
            var expoente = sorteio.Next(0, 40);

            Assert.Equal(
                BigInteger.Pow(a, expoente).ToString(),
                Meu(a).Elevar(expoente).ToString());

            var bits = sorteio.Next(0, 200);

            Assert.Equal((a << bits).ToString(), (Meu(a) << bits).ToString());

            // O `>>` do BigInteger arredonda para baixo em negativos (é
            // complemento de dois) e aqui ele desloca a magnitude. As duas
            // definições são legítimas; a comparação vale para os positivos.
            if (a.Sign >= 0)
            {
                Assert.Equal((a >> bits).ToString(), (Meu(a) >> bits).ToString());
            }

            var b = Sortear(sorteio, 128);

            Assert.Equal(
                BigInteger.GreatestCommonDivisor(a, b).ToString(),
                Numero.Mdc(Meu(a), Meu(b)).ToString());
        }
    }

    [Fact(DisplayName = "a potência modular, que é o coração do RSA")]
    public void PotenciaModular()
    {
        var sorteio = new Random(29);

        for (var i = 0; i < 2_000; i++)
        {
            var baseA = BigInteger.Abs(Sortear(sorteio, 128));
            var expoente = BigInteger.Abs(Sortear(sorteio, 64));
            var modulo = BigInteger.Abs(Sortear(sorteio, 128));

            if (modulo.IsZero)
            {
                continue;
            }

            Assert.Equal(
                BigInteger.ModPow(baseA, expoente, modulo).ToString(),
                Meu(baseA).ElevarModulo(Meu(expoente), Meu(modulo)).ToString());
        }
    }

    [Fact(DisplayName = "escrever e ler em todas as bases de 2 a 36")]
    public void TodasAsBases()
    {
        var sorteio = new Random(31);

        for (var i = 0; i < 5_000; i++)
        {
            var valor = Meu(Sortear(sorteio, 300));

            for (var baseDoTexto = 2; baseDoTexto <= 36; baseDoTexto++)
            {
                var texto = valor.Escrever(baseDoTexto);

                Assert.Equal(valor.ToString(), Numero.Ler(texto, baseDoTexto).ToString());
            }
        }
    }

    [Fact(DisplayName = "o decimal e o hexadecimal batem com os do .NET")]
    public void ODecimalEOHexa()
    {
        var sorteio = new Random(37);

        for (var i = 0; i < 20_000; i++)
        {
            var dele = Sortear(sorteio, 400);

            Assert.Equal(dele.ToString(), Meu(dele).ToString());

            // O hexadecimal do BigInteger vem em complemento de dois e com um
            // zero à frente quando o bit alto está ligado -- por isso a
            // comparação é pelo valor absoluto, lido de volta.
            var hexa = Meu(dele).Absoluto().Escrever(16);

            Assert.Equal(
                BigInteger.Abs(dele).ToString(),
                BigInteger.Parse("0" + hexa, System.Globalization.NumberStyles.HexNumber)
                    .ToString());
        }
    }

    [Fact(DisplayName = "os números que costumam quebrar tudo")]
    public void OsCasosDeBorda()
    {
        long[] casos =
        [
            0, 1, -1, 2, -2,
            int.MaxValue, int.MinValue,
            long.MaxValue, long.MinValue,
            (long)uint.MaxValue, (long)uint.MaxValue + 1, -(long)uint.MaxValue - 1,
            4294967295L, 4294967296L, 4294967297L,
        ];

        foreach (var a in casos)
        {
            // O `long.MinValue` é o clássico: ele não tem oposto positivo, e um
            // `-valor` na construção devolve o próprio número.
            Assert.Equal(a.ToString(), Numero.De(a).ToString());

            foreach (var b in casos)
            {
                var grandeA = new BigInteger(a);
                var grandeB = new BigInteger(b);

                Assert.Equal((grandeA + grandeB).ToString(),
                    (Numero.De(a) + Numero.De(b)).ToString());

                Assert.Equal((grandeA * grandeB).ToString(),
                    (Numero.De(a) * Numero.De(b)).ToString());

                if (b != 0)
                {
                    Assert.Equal(BigInteger.Divide(grandeA, grandeB).ToString(),
                        (Numero.De(a) / Numero.De(b)).ToString());
                }
            }
        }
    }

    [Fact(DisplayName = "zero é zero, e não há dois deles")]
    public void ZeroNaoTemSinal()
    {
        // Sem esta regra existiriam dois zeros, e `a == b` deixaria de valer
        // para eles -- o defeito mais chato de uma aritmética de sinal e
        // magnitude.
        var zeros = new[]
        {
            Numero.Zero,
            Numero.De(0),
            -Numero.Zero,
            Numero.De(5) - Numero.De(5),
            Numero.De(-5) + Numero.De(5),
            Numero.De(0) * Numero.De(-7),
            Numero.Ler("-0"),
            Numero.Ler("0"),
            Numero.De(7) % Numero.De(7),
        };

        foreach (var zero in zeros)
        {
            Assert.Equal(0, zero.Sinal);
            Assert.True(zero.EhZero);
            Assert.Equal("0", zero.ToString());
            Assert.Equal(Numero.Zero, zero);
            Assert.Equal(Numero.Zero.GetHashCode(), zero.GetHashCode());
        }
    }

    [Fact(DisplayName = "um fatorial de mil, dígito por dígito")]
    public void FatorialDeMil()
    {
        // 2.568 dígitos, e todos têm de estar certos. É o teste que mais
        // encontra defeito de "vai um" -- um erro de um único bit numa
        // multiplicação intermediária muda o número inteiro dali para a frente.
        var meu = Numero.Um;
        var dele = BigInteger.One;

        for (var i = 1; i <= 1_000; i++)
        {
            meu *= Numero.De(i);
            dele *= i;
        }

        Assert.Equal(dele.ToString(), meu.ToString());
        Assert.Equal(2568, meu.ToString().Length);
    }

    [Fact(DisplayName = "dois elevado a cem mil")]
    public void DoisElevadoACemMil()
    {
        var meu = Numero.Dois.Elevar(100_000);
        var dele = BigInteger.Pow(2, 100_000);

        Assert.Equal(dele.ToString(), meu.ToString());
        Assert.Equal(100_001, meu.QuantosBits);
    }

    [Fact(DisplayName = "a divisão por zero é erro, e não um número qualquer")]
    public void DivisaoPorZero()
    {
        Assert.Throws<DivideByZeroException>(() => Numero.De(1) / Numero.Zero);
        Assert.Throws<DivideByZeroException>(() => Numero.De(1) % Numero.Zero);
        Assert.Throws<DivideByZeroException>(
            () => Numero.De(2).ElevarModulo(Numero.De(3), Numero.Zero));
    }

    [Theory(DisplayName = "o que não é número é recusado")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("-")]
    [InlineData("+")]
    [InlineData("12a")]
    [InlineData("1.5")]
    [InlineData("1 2")]
    [InlineData("--1")]
    public void OQueNaoEhNumero(string texto)
    {
        Assert.ThrowsAny<Exception>(() => Numero.Ler(texto));
    }
}
