using System.Globalization;
using System.Text;

namespace Conde.NumeroGrande;

/// <summary>
/// Um inteiro de tamanho arbitrário, em sinal e magnitude.
/// </summary>
/// <remarks>
/// <para>
/// Sinal separado da magnitude, e não complemento de dois. A escolha é a mesma
/// que o <c>BigInteger</c> do Java faz e o oposto do que o do .NET faz, e vale a
/// pena dizer por quê.
/// </para>
/// <para>
/// Com sinal separado, somar dois números de sinais diferentes vira uma
/// subtração e uma comparação — três linhas de decisão. Com complemento de dois,
/// a soma é sempre a mesma conta e o sinal sai de graça, mas o número precisa de
/// extensão de sinal em toda operação que muda o tamanho, e a magnitude de um
/// negativo nunca é o que está no vetor.
/// </para>
/// <para>
/// Para uma biblioteca que quer ser lida, sinal e magnitude ganha: o vetor
/// guarda o que ele parece guardar.
/// </para>
/// <para>
/// E há uma regra que o construtor faz valer sempre: <b>zero não tem sinal
/// negativo</b>. Sem ela existiriam dois zeros, e <c>a == b</c> deixaria de
/// valer para eles.
/// </para>
/// </remarks>
public readonly struct Numero : IEquatable<Numero>, IComparable<Numero>
{
    private readonly uint[] _palavras;

    /// <summary>−1, 0 ou 1.</summary>
    public int Sinal { get; }

    private Numero(int sinal, uint[] palavras)
    {
        palavras = Magnitude.Aparar(palavras);

        // Zero é zero: sem sinal, sem palavras, sem duas representações.
        if (palavras.Length == 0)
        {
            Sinal = 0;
            _palavras = Magnitude.Zero;

            return;
        }

        Sinal = sinal;
        _palavras = palavras;
    }

    internal uint[] Palavras => _palavras ?? Magnitude.Zero;

    public static readonly Numero Zero = new(0, Magnitude.Zero);

    public static readonly Numero Um = new(1, [1]);

    public static readonly Numero Dois = new(1, [2]);

    public bool EhZero => Sinal == 0;

    public bool EhNegativo => Sinal < 0;

    public bool EhPar => Palavras.Length == 0 || (Palavras[0] & 1) == 0;

    /// <summary>Quantos bits a magnitude ocupa.</summary>
    public int QuantosBits => Magnitude.QuantosBits(Palavras);

    // -- construção --------------------------------------------------------

    public static Numero De(long valor)
    {
        if (valor == 0)
        {
            return Zero;
        }

        var sinal = valor < 0 ? -1 : 1;

        // O `long.MinValue` não tem oposto positivo, e um `-valor` aqui daria
        // o próprio número de volta. O caminho por `ulong` é o único que não
        // tem essa armadilha.
        var magnitude = valor < 0 ? (ulong)(-(valor + 1)) + 1 : (ulong)valor;

        return new Numero(sinal, [(uint)magnitude, (uint)(magnitude >> 32)]);
    }

    public static implicit operator Numero(long valor) => De(valor);

    /// <summary>Lê um número escrito em qualquer base de 2 a 36.</summary>
    public static Numero Ler(string texto, int baseDoTexto = 10)
    {
        if (baseDoTexto < 2 || baseDoTexto > 36)
        {
            throw new ArgumentOutOfRangeException(nameof(baseDoTexto),
                "a base tem de estar entre 2 e 36");
        }

        if (string.IsNullOrWhiteSpace(texto))
        {
            throw new FormatException("texto vazio");
        }

        var limpo = texto.Trim();
        var sinal = 1;
        var onde = 0;

        if (limpo[0] == '-')
        {
            sinal = -1;
            onde = 1;
        }
        else if (limpo[0] == '+')
        {
            onde = 1;
        }

        if (onde >= limpo.Length)
        {
            throw new FormatException("só o sinal, sem dígito nenhum");
        }

        var magnitude = Magnitude.Zero;
        var baseComoNumero = new uint[] { (uint)baseDoTexto };

        foreach (var letra in limpo[onde..])
        {
            var digito = Digito(letra);

            if (digito < 0 || digito >= baseDoTexto)
            {
                throw new FormatException(
                    $"'{letra}' não é um dígito na base {baseDoTexto}");
            }

            magnitude = Magnitude.Somar(
                Magnitude.Multiplicar(magnitude, baseComoNumero),
                digito == 0 ? Magnitude.Zero : [(uint)digito]);
        }

        return new Numero(sinal, magnitude);
    }

    private static int Digito(char letra) => letra switch
    {
        >= '0' and <= '9' => letra - '0',
        >= 'a' and <= 'z' => letra - 'a' + 10,
        >= 'A' and <= 'Z' => letra - 'A' + 10,
        _ => -1,
    };

    // -- as contas ---------------------------------------------------------

    public static Numero operator +(Numero a, Numero b)
    {
        if (a.EhZero)
        {
            return b;
        }

        if (b.EhZero)
        {
            return a;
        }

        if (a.Sinal == b.Sinal)
        {
            return new Numero(a.Sinal, Magnitude.Somar(a.Palavras, b.Palavras));
        }

        // Sinais diferentes: o maior em magnitude manda no sinal do resultado.
        var comparacao = Magnitude.Comparar(a.Palavras, b.Palavras);

        if (comparacao == 0)
        {
            return Zero;
        }

        return comparacao > 0
            ? new Numero(a.Sinal, Magnitude.Subtrair(a.Palavras, b.Palavras))
            : new Numero(b.Sinal, Magnitude.Subtrair(b.Palavras, a.Palavras));
    }

    public static Numero operator -(Numero a, Numero b) => a + (-b);

    public static Numero operator -(Numero a) =>
        a.EhZero ? Zero : new Numero(-a.Sinal, a.Palavras);

    public static Numero operator *(Numero a, Numero b)
    {
        if (a.EhZero || b.EhZero)
        {
            return Zero;
        }

        return new Numero(a.Sinal * b.Sinal, Magnitude.Multiplicar(a.Palavras, b.Palavras));
    }

    /// <summary>
    /// A divisão que trunca para zero, com o resto do mesmo sinal do dividendo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// É a convenção de C, de Java, de C# e do <c>BigInteger</c> do .NET:
    /// <c>-7 / 2</c> é <c>-3</c> e <c>-7 % 2</c> é <c>-1</c>. Não é a única
    /// razoável — Python arredonda para baixo e dá <c>-4</c> e <c>1</c> —, e a
    /// diferença aparece o tempo todo em índices circulares.
    /// </para>
    /// <para>
    /// A escolha aqui é a do juiz, porque ser diferente dele por gosto próprio
    /// só cria armadilha para quem usa os dois.
    /// </para>
    /// </remarks>
    public static Numero operator /(Numero a, Numero b) => Dividir(a, b).Quociente;

    public static Numero operator %(Numero a, Numero b) => Dividir(a, b).Resto;

    public static (Numero Quociente, Numero Resto) Dividir(Numero a, Numero b)
    {
        if (b.EhZero)
        {
            throw new DivideByZeroException("divisão por zero");
        }

        if (a.EhZero)
        {
            return (Zero, Zero);
        }

        var (quociente, resto) = Divisao.Dividir(a.Palavras, b.Palavras);

        return (new Numero(a.Sinal * b.Sinal, quociente),
                new Numero(a.Sinal, resto));
    }

    /// <summary>O valor absoluto.</summary>
    public Numero Absoluto() => EhNegativo ? -this : this;

    /// <summary>
    /// Potência por quadrados repetidos.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Multiplicar <c>n</c> vezes custa <c>n</c> multiplicações; ir pelos bits
    /// do expoente custa o logaritmo disso. Para <c>2^1000</c> são dez
    /// multiplicações em vez de mil, e para <c>2^1000000</c> são vinte em vez
    /// de um milhão.
    /// </para>
    /// <para>
    /// A ideia é de antes de Cristo: aparece no <i>Chandaḥśāstra</i> de
    /// Pingala, na Índia, por volta de 200 a.C., para contar métricas poéticas.
    /// </para>
    /// </remarks>
    public Numero Elevar(int expoente)
    {
        if (expoente < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expoente),
                "expoente negativo dá fração, e isto aqui é inteiro");
        }

        if (expoente == 0)
        {
            return Um;
        }

        var resultado = Um;
        var baseAtual = this;

        while (expoente > 0)
        {
            if ((expoente & 1) != 0)
            {
                resultado *= baseAtual;
            }

            expoente >>= 1;

            if (expoente > 0)
            {
                baseAtual *= baseAtual;
            }
        }

        return resultado;
    }

    /// <summary>
    /// Potência modular: a conta que segura a criptografia de chave pública.
    /// </summary>
    /// <remarks>
    /// O resto é tirado a cada passo, e é isso que torna a conta possível:
    /// <c>7^1000000 mod 13</c> sem o resto no meio geraria um número de
    /// oitocentos mil dígitos; com ele, nenhum intermediário passa do dobro do
    /// módulo. É o coração do RSA e do Diffie-Hellman.
    /// </remarks>
    public Numero ElevarModulo(Numero expoente, Numero modulo)
    {
        if (modulo.EhZero)
        {
            throw new DivideByZeroException("módulo zero");
        }

        if (expoente.EhNegativo)
        {
            throw new ArgumentOutOfRangeException(nameof(expoente),
                "expoente negativo precisa de inverso modular, que não está aqui");
        }

        var resultado = Um;
        var baseAtual = this % modulo;

        if (baseAtual.EhNegativo)
        {
            baseAtual += modulo.Absoluto();
        }

        for (var bit = 0; bit < expoente.QuantosBits; bit++)
        {
            if (Magnitude.BitLigado(expoente.Palavras, bit))
            {
                resultado = resultado * baseAtual % modulo;
            }

            baseAtual = baseAtual * baseAtual % modulo;
        }

        return resultado;
    }

    /// <summary>
    /// O máximo divisor comum, por Euclides.
    /// </summary>
    /// <remarks>
    /// O algoritmo mais antigo que continua em uso: está nos <i>Elementos</i>,
    /// livro VII, proposição 2, de uns 300 anos antes de Cristo. Dois mil e
    /// trezentos anos e ninguém achou nada melhor para números em geral.
    /// </remarks>
    public static Numero Mdc(Numero a, Numero b)
    {
        a = a.Absoluto();
        b = b.Absoluto();

        while (!b.EhZero)
        {
            (a, b) = (b, a % b);
        }

        return a;
    }

    public static Numero operator <<(Numero a, int bits) =>
        bits < 0
            ? a >> -bits
            : new Numero(a.Sinal, Magnitude.DeslocarBitsEsquerda(a.Palavras, bits));

    public static Numero operator >>(Numero a, int bits) =>
        bits < 0
            ? a << -bits
            : new Numero(a.Sinal, Magnitude.DeslocarBitsDireita(a.Palavras, bits));

    // -- comparação --------------------------------------------------------

    public int CompareTo(Numero outro)
    {
        if (Sinal != outro.Sinal)
        {
            return Sinal < outro.Sinal ? -1 : 1;
        }

        var comparacao = Magnitude.Comparar(Palavras, outro.Palavras);

        // Entre dois negativos, a maior magnitude é o menor número.
        return Sinal < 0 ? -comparacao : comparacao;
    }

    public bool Equals(Numero outro) => CompareTo(outro) == 0;

    public override bool Equals(object? outro) => outro is Numero numero && Equals(numero);

    public override int GetHashCode()
    {
        var codigo = Sinal;

        foreach (var palavra in Palavras)
        {
            codigo = codigo * 31 + (int)palavra;
        }

        return codigo;
    }

    public static bool operator ==(Numero a, Numero b) => a.Equals(b);

    public static bool operator !=(Numero a, Numero b) => !a.Equals(b);

    public static bool operator <(Numero a, Numero b) => a.CompareTo(b) < 0;

    public static bool operator >(Numero a, Numero b) => a.CompareTo(b) > 0;

    public static bool operator <=(Numero a, Numero b) => a.CompareTo(b) <= 0;

    public static bool operator >=(Numero a, Numero b) => a.CompareTo(b) >= 0;

    // -- impressão ---------------------------------------------------------

    public override string ToString() => Escrever(10);

    /// <summary>
    /// Escreve o número numa base de 2 a 36.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Converter para decimal é a operação mais cara que um número grande tem,
    /// e o motivo é que 10 não é potência de 2: cada dígito decimal exige uma
    /// divisão do número inteiro. Escrever um número de um milhão de dígitos em
    /// hexadecimal é uma varredura; em decimal, é um milhão de divisões.
    /// </para>
    /// <para>
    /// O truque que reduz isso em nove vezes: dividir por 10⁹ de uma vez — a
    /// maior potência de 10 que cabe numa palavra — e tirar nove dígitos de cada
    /// divisão. Continua caro, e fica nove vezes menos caro.
    /// </para>
    /// </remarks>
    public string Escrever(int baseDoTexto)
    {
        if (baseDoTexto < 2 || baseDoTexto > 36)
        {
            throw new ArgumentOutOfRangeException(nameof(baseDoTexto));
        }

        if (EhZero)
        {
            return "0";
        }

        var digitos = new StringBuilder();
        var resto = Palavras;

        // A maior potência da base que cabe numa palavra de 32 bits.
        var porVez = 1u;
        var quantosPorVez = 0;

        while ((ulong)porVez * (ulong)baseDoTexto < uint.MaxValue)
        {
            porVez *= (uint)baseDoTexto;
            quantosPorVez++;
        }

        while (resto.Length > 0)
        {
            var (quociente, sobra) = Divisao.Dividir(resto, [porVez]);

            var pedaco = sobra.Length == 0 ? 0u : sobra[0];

            resto = quociente;

            for (var i = 0; i < quantosPorVez; i++)
            {
                if (resto.Length == 0 && pedaco == 0)
                {
                    break;
                }

                digitos.Append(Letra((int)(pedaco % (uint)baseDoTexto)));
                pedaco /= (uint)baseDoTexto;
            }
        }

        if (digitos.Length == 0)
        {
            digitos.Append('0');
        }

        if (EhNegativo)
        {
            digitos.Append('-');
        }

        // Os dígitos saíram do menos significativo para o mais: é só virar.
        var saida = digitos.ToString().ToCharArray();

        Array.Reverse(saida);

        return new string(saida);
    }

    private static char Letra(int digito) =>
        (char)(digito < 10 ? '0' + digito : 'a' + digito - 10);

    /// <summary>O valor como <c>long</c>, se couber.</summary>
    public bool TentarComoLong(out long valor)
    {
        valor = 0;

        if (EhZero)
        {
            return true;
        }

        if (Palavras.Length > 2)
        {
            return false;
        }

        var magnitude = (ulong)Palavras[0];

        if (Palavras.Length == 2)
        {
            magnitude |= (ulong)Palavras[1] << 32;
        }

        if (EhNegativo)
        {
            if (magnitude > (ulong)long.MaxValue + 1)
            {
                return false;
            }

            valor = magnitude == (ulong)long.MaxValue + 1
                ? long.MinValue
                : -(long)magnitude;

            return true;
        }

        if (magnitude > long.MaxValue)
        {
            return false;
        }

        valor = (long)magnitude;

        return true;
    }
}
