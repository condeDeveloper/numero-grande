using System.Diagnostics;
using System.Numerics;
using Conde.NumeroGrande;

namespace Conde.NumeroGrande.Ferramentas;

/// <summary>
/// Mede o ponto em que Karatsuba passa a valer a pena — e o compara com o .NET.
/// </summary>
/// <remarks>
/// <para>
/// A constante <c>LimiteDeKaratsuba</c> existe em toda biblioteca de números
/// grandes, e em todas com um valor diferente: 32 no OpenJDK, 40 no
/// <c>libgmp</c> conforme a arquitetura, 70 em algumas versões do Python. Não é
/// desleixo — o valor depende da máquina, da linguagem e do compilador, e
/// copiar o de outra biblioteca é copiar a máquina de outra pessoa.
/// </para>
/// <para>
/// Esta ferramenta mede o valor <b>desta</b> máquina. Ela roda as duas
/// multiplicações em números de tamanhos crescentes e mostra onde a curva
/// cruza.
/// </para>
/// </remarks>
public static class Programa
{
    /// <summary>O limite usado durante a medida da virada.</summary>
    private static int LimiteMedido { get; set; } = 128;

    public static int Main(string[] argumentos)
    {
        var comando = argumentos.Length > 0 ? argumentos[0] : "tudo";

        if (comando is "tudo" or "virada")
        {
            Virada();
        }

        if (comando is "tudo" or "limite")
        {
            Console.WriteLine();
            Limite();
        }

        if (comando is "tudo" or "contra")
        {
            Console.WriteLine();
            ContraODotnet();
        }

        return 0;
    }

    private static uint[] Sortear(Random sorteio, int palavras)
    {
        var numero = new uint[palavras];

        for (var i = 0; i < palavras; i++)
        {
            numero[i] = (uint)sorteio.Next(int.MinValue, int.MaxValue);
        }

        // A palavra do topo não pode ser zero, senão o tamanho medido é menor.
        numero[^1] |= 0x8000_0000;

        return numero;
    }

    private static void Virada()
    {
        Console.WriteLine("onde Karatsuba passa a ganhar da conta de escola");
        Console.WriteLine();
        Console.WriteLine("palavras".PadLeft(10) + "bits".PadLeft(10)
            + "escola".PadLeft(12) + "karatsuba".PadLeft(12) + "  quem ganha");
        Console.WriteLine(new string('-', 60));

        var sorteio = new Random(20260929);

        var virada = -1;

        foreach (var palavras in (int[])
                 [4, 8, 16, 24, 32, 48, 64, 96, 128, 256, 512, 1024])
        {
            var a = Sortear(sorteio, palavras);
            var b = Sortear(sorteio, palavras);

            // Quantas repetições para a medida não ser ruído: quanto menor o
            // número, mais vezes é preciso repetir para o relógio enxergar.
            var vezes = Math.Max(30, 2_000_000 / (palavras * palavras));

            var deEscola = Cronometrar(vezes, () => Magnitude.MultiplicarDeEscola(a, b));

            var antes = Magnitude.LimiteDeKaratsuba;

            // O limite fica no valor de producao: o que se mede e o algoritmo
            // COMO ELE E USADO, com a conta de escola no fundo da recursao.
            // Medir Karatsuba puro ate duas palavras mede uma coisa que
            // ninguem roda.
            Magnitude.LimiteDeKaratsuba = LimiteMedido;

            var deKaratsuba = Cronometrar(vezes, () => Magnitude.Multiplicar(a, b));

            Magnitude.LimiteDeKaratsuba = antes;

            var ganhador = deKaratsuba < deEscola ? "karatsuba" : "escola";

            if (virada < 0 && deKaratsuba < deEscola)
            {
                virada = palavras;
            }

            Console.WriteLine(
                palavras.ToString().PadLeft(10)
                + (palavras * 32).ToString().PadLeft(10)
                + $"{deEscola:F3} ms".PadLeft(12)
                + $"{deKaratsuba:F3} ms".PadLeft(12)
                + $"  {ganhador}");
        }

        Console.WriteLine();
        Console.WriteLine(virada > 0
            ? $"a virada está por volta de {virada} palavras "
              + $"({virada * 32} bits, uns {virada * 32 * 0.301:F0} dígitos decimais)."
            : "Karatsuba não ganhou em nenhum tamanho medido.");

        Console.WriteLine($"o limite no código está em {Magnitude.LimiteDeKaratsuba} palavras.");
    }

    /// <summary>
    /// Varre os limites possíveis e mostra qual é o melhor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// É a pergunta certa, e a primeira versão desta ferramenta fazia a errada.
    /// Ela media "Karatsuba com o limite tal ganha da conta de escola?", e a
    /// resposta era não em todos os tamanhos — o que não diz nada sobre qual
    /// limite usar.
    /// </para>
    /// <para>
    /// A pergunta certa é: para um número deste tamanho, qual limite dá o menor
    /// tempo? Um limite muito baixo desce demais na recursão e paga montagem em
    /// cada nível; um muito alto vira a conta de escola pura. O melhor está no
    /// meio, e ele é diferente para cada tamanho.
    /// </para>
    /// </remarks>
    private static void Limite()
    {
        Console.WriteLine("qual limite dá o menor tempo, por tamanho");
        Console.WriteLine();

        int[] limites = [8, 16, 32, 64, 128, 256, 512, 1024, int.MaxValue];

        Console.Write("palavras".PadLeft(10));

        foreach (var limite in limites)
        {
            Console.Write((limite == int.MaxValue ? "escola" : limite.ToString()).PadLeft(10));
        }

        Console.WriteLine("   melhor");
        Console.WriteLine(new string('-', 10 + limites.Length * 10 + 12));

        var sorteio = new Random(20260929);

        var melhorGeral = int.MaxValue;

        foreach (var palavras in (int[])[64, 128, 256, 512, 1024, 2048])
        {
            var a = Sortear(sorteio, palavras);
            var b = Sortear(sorteio, palavras);

            var vezes = Math.Max(40, 4_000_000 / (palavras * palavras));

            Console.Write(palavras.ToString().PadLeft(10));

            var melhorTempo = double.MaxValue;
            var melhorLimite = int.MaxValue;

            var antes = Magnitude.LimiteDeKaratsuba;

            foreach (var limite in limites)
            {
                Magnitude.LimiteDeKaratsuba = limite;

                var gasto = Cronometrar(vezes, () => Magnitude.Multiplicar(a, b));

                Console.Write($"{gasto:F3}".PadLeft(10));

                if (gasto < melhorTempo)
                {
                    melhorTempo = gasto;
                    melhorLimite = limite;
                }
            }

            Magnitude.LimiteDeKaratsuba = antes;

            Console.WriteLine("   "
                + (melhorLimite == int.MaxValue ? "escola" : melhorLimite.ToString()));

            if (palavras >= 512 && melhorLimite != int.MaxValue)
            {
                melhorGeral = Math.Min(melhorGeral, melhorLimite);
            }
        }

        Console.WriteLine();
        Console.WriteLine("(tempos em milissegundos)");
        Console.WriteLine();
        Console.WriteLine(melhorGeral == int.MaxValue
            ? "nenhum limite de Karatsuba ganhou da conta de escola pura nesta máquina."
            : $"o melhor limite para números grandes é {melhorGeral} palavras.");
    }

    private static void ContraODotnet()
    {
        Console.WriteLine("contra o System.Numerics.BigInteger");
        Console.WriteLine();
        Console.WriteLine("operação".PadRight(22) + "dígitos".PadLeft(10)
            + "meu".PadLeft(12) + ".NET".PadLeft(12) + "razão".PadLeft(10));
        Console.WriteLine(new string('-', 68));

        foreach (var digitos in (int[])[100, 1_000, 10_000])
        {
            var textoA = Texto(digitos, 1);
            var textoB = Texto(digitos, 2);

            var meuA = Numero.Ler(textoA);
            var meuB = Numero.Ler(textoB);

            var deleA = BigInteger.Parse(textoA);
            var deleB = BigInteger.Parse(textoB);

            var vezes = Math.Max(3, 2_000_000 / (digitos * digitos / 10 + 1));

            Linha("multiplicar", digitos, vezes,
                () => _ = meuA * meuB,
                () => _ = deleA * deleB);

            Linha("dividir", digitos, vezes,
                () => _ = meuA / meuB,
                () => _ = deleA / deleB);

            Linha("escrever em decimal", digitos, Math.Max(3, vezes / 10),
                () => _ = meuA.ToString(),
                () => _ = deleA.ToString());
        }

        Console.WriteLine();
        Console.WriteLine("Não é para ganhar: o BigInteger do .NET tem anos de");
        Console.WriteLine("otimização e caminhos em código nativo. É para saber a");
        Console.WriteLine("distância -- e uma razão de duas ou três vezes quer dizer");
        Console.WriteLine("que o algoritmo está certo e falta o acabamento.");
    }

    private static void Linha(string nome, int digitos, int vezes,
        Action meu, Action dele)
    {
        var gastoMeu = Cronometrar(vezes, meu);
        var gastoDele = Cronometrar(vezes, dele);

        var razao = gastoDele > 0 ? gastoMeu / gastoDele : 0;

        Console.WriteLine(nome.PadRight(22)
            + digitos.ToString().PadLeft(10)
            + $"{gastoMeu:F3} ms".PadLeft(12)
            + $"{gastoDele:F3} ms".PadLeft(12)
            + $"{razao:F1}x".PadLeft(10));
    }

    private static string Texto(int digitos, int semente)
    {
        var sorteio = new Random(semente);
        var texto = new System.Text.StringBuilder();

        texto.Append((char)('1' + sorteio.Next(9)));

        for (var i = 1; i < digitos; i++)
        {
            texto.Append((char)('0' + sorteio.Next(10)));
        }

        return texto.ToString();
    }

    /// <summary>Roda a ação `vezes` vezes e devolve a média em milissegundos.</summary>
    private static double Cronometrar(int vezes, Action acao)
    {
        // Uma passada de aquecimento: sem ela, a primeira medição carrega o
        // custo de compilar o método, que costuma ser maior que o que se quer
        // medir.
        acao();

        var relogio = Stopwatch.StartNew();

        for (var i = 0; i < vezes; i++)
        {
            acao();
        }

        return relogio.Elapsed.TotalMilliseconds / vezes;
    }
}
