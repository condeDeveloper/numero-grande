namespace Conde.NumeroGrande;

/// <summary>
/// A aritmética sem sinal, sobre vetores de palavras de 32 bits.
/// </summary>
/// <remarks>
/// <para>
/// Um número grande é um vetor de dígitos numa base grande. A escolha da base é
/// a primeira decisão do projeto, e ela é menos livre do que parece.
/// </para>
/// <para>
/// A base aqui é <b>2³²</b>, com cada dígito num <c>uint</c>. O motivo não é
/// estética: a soma de dois dígitos com o "vai um" precisa caber num tipo que a
/// máquina saiba somar de uma vez, e o produto de dois dígitos precisa caber em
/// <b>dois</b>. Com base 2³² os dois cabem num <c>ulong</c>, e o processador faz
/// a conta numa instrução. Com base 2⁶⁴ o produto precisaria de 128 bits, que o
/// C# só ganhou em 2024 — e aí a portabilidade acaba.
/// </para>
/// <para>
/// Uma base decimal (10⁹, por exemplo) tornaria a impressão trivial e a
/// aritmética duas vezes mais lenta. Vale para uma calculadora de bolso e não
/// para uma biblioteca: imprimir acontece uma vez, somar acontece milhões.
/// </para>
/// <para>
/// A convenção, aqui e em toda parte: o dígito <b>menos</b> significativo vem
/// primeiro. É ao contrário de como se escreve um número no papel, e é a ordem
/// certa para o código — a soma começa pelas unidades, e crescer o vetor é
/// acrescentar no fim.
/// </para>
/// </remarks>
public static class Magnitude
{
    public static readonly uint[] Zero = [];

    /// <summary>Tira os zeros da ponta alta.</summary>
    /// <remarks>
    /// Toda operação termina aqui. Sem a normalização, <c>2 - 1</c> daria um
    /// vetor <c>[1, 0]</c> que representa 1 e não é igual ao vetor <c>[1]</c> —
    /// e a comparação por tamanho, que é a barata, deixaria de valer.
    /// </remarks>
    public static uint[] Aparar(uint[] palavras)
    {
        var quantas = palavras.Length;

        while (quantas > 0 && palavras[quantas - 1] == 0)
        {
            quantas--;
        }

        if (quantas == palavras.Length)
        {
            return palavras;
        }

        if (quantas == 0)
        {
            return Zero;
        }

        var aparadas = new uint[quantas];

        Array.Copy(palavras, aparadas, quantas);

        return aparadas;
    }

    public static int Comparar(uint[] a, uint[] b)
    {
        if (a.Length != b.Length)
        {
            return a.Length < b.Length ? -1 : 1;
        }

        // Do dígito mais significativo para o menos: o primeiro que diferir
        // decide, e na maioria das comparações isso é a primeira iteração.
        for (var i = a.Length - 1; i >= 0; i--)
        {
            if (a[i] != b[i])
            {
                return a[i] < b[i] ? -1 : 1;
            }
        }

        return 0;
    }

    public static uint[] Somar(uint[] a, uint[] b)
    {
        if (a.Length < b.Length)
        {
            (a, b) = (b, a);
        }

        var saida = new uint[a.Length + 1];
        ulong vaiUm = 0;

        for (var i = 0; i < a.Length; i++)
        {
            // O `ulong` é o que faz isto funcionar: a soma de dois `uint` mais
            // o "vai um" cabe em 33 bits, e o de cima é o vai-um seguinte.
            var soma = (ulong)a[i] + (i < b.Length ? b[i] : 0u) + vaiUm;

            saida[i] = (uint)soma;
            vaiUm = soma >> 32;
        }

        saida[a.Length] = (uint)vaiUm;

        return Aparar(saida);
    }

    /// <summary>Subtrai, exigindo que <c>a &gt;= b</c>.</summary>
    /// <remarks>
    /// Exigir em vez de aceitar é a decisão certa. Quem conhece os sinais é a
    /// camada de cima, e uma subtração sem sinal que devolvesse "o valor
    /// absoluto da diferença" esconderia um erro de lógica no lugar de
    /// denunciá-lo.
    /// </remarks>
    public static uint[] Subtrair(uint[] a, uint[] b)
    {
        var saida = new uint[a.Length];
        long empresta = 0;

        for (var i = 0; i < a.Length; i++)
        {
            var conta = (long)a[i] - (i < b.Length ? b[i] : 0u) - empresta;

            if (conta < 0)
            {
                conta += 1L << 32;
                empresta = 1;
            }
            else
            {
                empresta = 0;
            }

            saida[i] = (uint)conta;
        }

        if (empresta != 0)
        {
            throw new InvalidOperationException(
                "subtração sem sinal com o menor em cima: quem cuida do sinal é a "
                + "camada de fora");
        }

        return Aparar(saida);
    }

    /// <summary>
    /// A multiplicação de escola: cada dígito de um por todos do outro.
    /// </summary>
    /// <remarks>
    /// <para>
    /// É a conta que se aprende no primário, com a base trocada. O custo é o
    /// produto dos tamanhos, e para números pequenos ela ganha de qualquer
    /// algoritmo esperto — porque não tem custo de montagem.
    /// </para>
    /// <para>
    /// O acumulador em <c>ulong</c> segura o produto de dois dígitos (64 bits)
    /// mais o que já estava lá mais o vai-um, e isso <b>cabe</b>: o maior
    /// produto é (2³²−1)², e sobra espaço para as duas somas. É o cálculo que
    /// justifica a base escolhida, e errá-lo dá um resultado que só diverge em
    /// números grandes.
    /// </para>
    /// </remarks>
    public static uint[] MultiplicarDeEscola(uint[] a, uint[] b)
    {
        if (a.Length == 0 || b.Length == 0)
        {
            return Zero;
        }

        var saida = new uint[a.Length + b.Length];

        for (var i = 0; i < a.Length; i++)
        {
            ulong vaiUm = 0;
            var atual = a[i];

            if (atual == 0)
            {
                continue;
            }

            for (var j = 0; j < b.Length; j++)
            {
                var conta = (ulong)atual * b[j] + saida[i + j] + vaiUm;

                saida[i + j] = (uint)conta;
                vaiUm = conta >> 32;
            }

            // O vai-um que sobra entra na palavra seguinte, e ela nunca estoura
            // porque o resultado cabe em a.Length + b.Length palavras.
            saida[i + b.Length] = (uint)vaiUm;
        }

        return Aparar(saida);
    }

    /// <summary>
    /// A partir de quantas palavras vale a pena usar Karatsuba.
    /// </summary>
    /// <remarks>
    /// <para>
    /// O número não é chutado: ele saiu da ferramenta de medida, que varre os
    /// limites possíveis e mostra qual dá o menor tempo em cada tamanho.
    /// </para>
    /// <para>
    /// E ele foi uma surpresa. A primeira versão usava <b>32</b>, que é o valor
    /// do OpenJDK — copiado com a confiança de quem acha que uma constante
    /// dessas é universal. A medida:
    /// </para>
    /// <code>
    ///   tamanho          limite 32   limite 128   conta de escola
    ///   1024 palavras     0,618 ms     0,440 ms          0,997 ms
    ///   2048 palavras     1,673 ms     1,319 ms          3,478 ms
    /// </code>
    /// <para>
    /// O valor copiado deixava a multiplicação <b>40% mais lenta</b> do que ela
    /// podia ser. O motivo é que o limite certo depende de quanto custa
    /// <i>montar</i> um nível de recursão, e isso depende da linguagem, do
    /// coletor de lixo e da implementação — esta aloca mais por nível que a do
    /// OpenJDK, então precisa de pedaços maiores para compensar.
    /// </para>
    /// <para>
    /// Copiar a constante de outra biblioteca é copiar a máquina de outra
    /// pessoa. Com o valor medido, Karatsuba fica <b>2,3 vezes</b> mais rápido
    /// que a conta de escola em números de 32 mil bits, e <b>2,6</b> em 65 mil.
    /// </para>
    /// <para>
    /// E a medida só ficou confiável depois de subir as repetições: com três
    /// passadas por ponto, o ruído chegava a inverter o vencedor entre duas
    /// execuções seguidas. Uma medida instável é pior que nenhuma — ela dá
    /// confiança no número errado.
    /// </para>
    /// </remarks>
    public static int LimiteDeKaratsuba { get; set; } = 128;

    /// <summary>
    /// Karatsuba: três multiplicações onde a conta de escola faz quatro.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Partindo cada número em duas metades, <c>a = a₁·B + a₀</c> e
    /// <c>b = b₁·B + b₀</c>, o produto é:
    /// </para>
    /// <code>
    ///   a·b = a₁b₁·B² + (a₁b₀ + a₀b₁)·B + a₀b₀
    /// </code>
    /// <para>
    /// São quatro multiplicações. Karatsuba percebeu, em 1960, que o termo do
    /// meio sai dos outros dois com <b>uma</b> multiplicação a mais em vez de
    /// duas:
    /// </para>
    /// <code>
    ///   a₁b₀ + a₀b₁ = (a₁ + a₀)(b₁ + b₀) − a₁b₁ − a₀b₀
    /// </code>
    /// <para>
    /// Três multiplicações de metade do tamanho, em vez de quatro. O custo cai
    /// de n² para n^1,585 — e a história é boa: Kolmogorov havia conjecturado,
    /// num seminário de 1960, que n² era o mínimo. Karatsuba, com 23 anos, o
    /// refutou em uma semana. Kolmogorov publicou o resultado em nome dele e
    /// encerrou o seminário.
    /// </para>
    /// </remarks>
    public static uint[] Multiplicar(uint[] a, uint[] b)
    {
        if (a.Length == 0 || b.Length == 0)
        {
            return Zero;
        }

        if (Math.Min(a.Length, b.Length) < LimiteDeKaratsuba)
        {
            return MultiplicarDeEscola(a, b);
        }

        var meio = Math.Max(a.Length, b.Length) / 2;

        var aBaixo = Fatiar(a, 0, meio);
        var aCima = Fatiar(a, meio, a.Length - meio);
        var bBaixo = Fatiar(b, 0, meio);
        var bCima = Fatiar(b, meio, b.Length - meio);

        var deBaixo = Multiplicar(aBaixo, bBaixo);
        var deCima = Multiplicar(aCima, bCima);

        // O termo do meio, pela identidade de Karatsuba.
        var somas = Multiplicar(Somar(aBaixo, aCima), Somar(bBaixo, bCima));
        var doMeio = Subtrair(Subtrair(somas, deBaixo), deCima);

        // A junta dos tres termos e feita NUM vetor so, somando no lugar.
        //
        // A versao obvia -- deslocar cada termo e somar os tres -- aloca quatro
        // vetores grandes por nivel de recursao, e com dez niveis isso e milhares
        // de alocacoes por multiplicacao. A ferramenta de medida mostrou o
        // estrago: com ela, Karatsuba perdia da conta de escola em TODOS os
        // tamanhos medidos, ate 32 mil bits.
        var saida = new uint[a.Length + b.Length + 1];

        SomarEm(saida, 0, deBaixo);
        SomarEm(saida, meio, doMeio);
        SomarEm(saida, meio * 2, deCima);

        return Aparar(saida);
    }

    /// <summary>Soma <paramref name="valor"/> dentro de um vetor, a partir de um deslocamento.</summary>
    private static void SomarEm(uint[] saida, int onde, uint[] valor)
    {
        ulong vaiUm = 0;
        var i = 0;

        for (; i < valor.Length; i++)
        {
            var soma = (ulong)saida[onde + i] + valor[i] + vaiUm;

            saida[onde + i] = (uint)soma;
            vaiUm = soma >> 32;
        }

        // O vai-um pode atravessar varias palavras de zeros adiante.
        while (vaiUm != 0)
        {
            var soma = (ulong)saida[onde + i] + vaiUm;

            saida[onde + i] = (uint)soma;
            vaiUm = soma >> 32;

            i++;
        }
    }

    private static uint[] Fatiar(uint[] palavras, int de, int quantas)
    {
        if (de >= palavras.Length || quantas <= 0)
        {
            return Zero;
        }

        quantas = Math.Min(quantas, palavras.Length - de);

        var pedaco = new uint[quantas];

        Array.Copy(palavras, de, pedaco, 0, quantas);

        return Aparar(pedaco);
    }

    /// <summary>Multiplica por B^n — que é só empurrar as palavras.</summary>
    public static uint[] DeslocarPalavras(uint[] palavras, int quantas)
    {
        if (palavras.Length == 0 || quantas == 0)
        {
            return palavras;
        }

        var saida = new uint[palavras.Length + quantas];

        Array.Copy(palavras, 0, saida, quantas, palavras.Length);

        return saida;
    }

    public static uint[] DeslocarBitsEsquerda(uint[] palavras, int bits)
    {
        if (palavras.Length == 0 || bits == 0)
        {
            return palavras;
        }

        var dePalavras = bits / 32;
        var deBits = bits % 32;

        if (deBits == 0)
        {
            return DeslocarPalavras(palavras, dePalavras);
        }

        var saida = new uint[palavras.Length + dePalavras + 1];

        for (var i = 0; i < palavras.Length; i++)
        {
            var conta = (ulong)palavras[i] << deBits;

            saida[i + dePalavras] |= (uint)conta;
            saida[i + dePalavras + 1] = (uint)(conta >> 32);
        }

        return Aparar(saida);
    }

    public static uint[] DeslocarBitsDireita(uint[] palavras, int bits)
    {
        var dePalavras = bits / 32;
        var deBits = bits % 32;

        if (dePalavras >= palavras.Length)
        {
            return Zero;
        }

        var quantas = palavras.Length - dePalavras;
        var saida = new uint[quantas];

        for (var i = 0; i < quantas; i++)
        {
            var conta = (ulong)palavras[i + dePalavras] >> deBits;

            if (deBits > 0 && i + dePalavras + 1 < palavras.Length)
            {
                conta |= (ulong)palavras[i + dePalavras + 1] << (32 - deBits);
            }

            saida[i] = (uint)conta;
        }

        return Aparar(saida);
    }

    public static int QuantosBits(uint[] palavras)
    {
        if (palavras.Length == 0)
        {
            return 0;
        }

        return palavras.Length * 32
            - System.Numerics.BitOperations.LeadingZeroCount(palavras[^1]);
    }

    public static bool BitLigado(uint[] palavras, int qual)
    {
        var palavra = qual / 32;

        return palavra < palavras.Length && (palavras[palavra] & (1u << (qual % 32))) != 0;
    }
}
