namespace Conde.NumeroGrande;

/// <summary>
/// A divisão longa — o algoritmo D de Knuth.
/// </summary>
/// <remarks>
/// <para>
/// De todas as operações de um número grande, a divisão é a única difícil. Somar
/// e subtrair são um laço; multiplicar é dois laços; dividir é o algoritmo que
/// Knuth gastou dez páginas descrevendo no volume 2 do <i>The Art of Computer
/// Programming</i>, com duas correções que quase ninguém acerta de primeira.
/// </para>
/// <para>
/// O problema é o palpite. A divisão longa funciona adivinhando um dígito do
/// quociente de cada vez, e na base 10 a gente adivinha olhando: "cabe umas três
/// vezes". Na base 2³² não dá para olhar — o palpite tem de sair de uma conta.
/// </para>
/// <para>
/// A conta é dividir os <b>dois</b> dígitos mais altos do que resta pelo dígito
/// mais alto do divisor. Knuth provou que esse palpite erra no máximo <b>dois</b>
/// para cima e nunca para baixo — desde que o divisor esteja normalizado, com o
/// bit mais alto ligado. Daí os dois passos que parecem sobra e não são:
/// </para>
/// <list type="number">
///   <item><description>
///     <b>A normalização.</b> Os dois números são deslocados para a esquerda até
///     o divisor ter o bit mais alto ligado. Isso não muda o quociente — muda só
///     o resto, que é desfeito no fim — e é o que faz a garantia dos "dois para
///     cima" valer.
///   </description></item>
///   <item><description>
///     <b>A correção.</b> Depois de subtrair, se o resultado ficou negativo, o
///     palpite era grande demais: devolve-se o divisor e desconta-se um do
///     dígito. Acontece em menos de dois por mil dos casos, e é exatamente por
///     ser raro que ela costuma passar despercebida — o código funciona em todos
///     os testes escritos à mão e erra num número de mil dígitos.
///   </description></item>
/// </list>
/// </remarks>
internal static class Divisao
{
    public static (uint[] Quociente, uint[] Resto) Dividir(uint[] a, uint[] b)
    {
        if (b.Length == 0)
        {
            throw new DivideByZeroException("divisão por zero");
        }

        if (Magnitude.Comparar(a, b) < 0)
        {
            return (Magnitude.Zero, a);
        }

        if (b.Length == 1)
        {
            return DividirPorUm(a, b[0]);
        }

        return AlgoritmoD(a, b);
    }

    /// <summary>
    /// Divisor de uma palavra só: um laço, sem palpite nenhum.
    /// </summary>
    /// <remarks>
    /// Vale o caso à parte porque ele é o mais comum de todos — converter para
    /// decimal divide por 10⁹ repetidamente — e porque com divisor de uma
    /// palavra o resto parcial cabe num <c>ulong</c> e o processador faz a
    /// divisão numa instrução. O algoritmo D aqui seria dez vezes mais lento
    /// para dar o mesmo resultado.
    /// </remarks>
    private static (uint[], uint[]) DividirPorUm(uint[] a, uint divisor)
    {
        var quociente = new uint[a.Length];
        ulong resto = 0;

        for (var i = a.Length - 1; i >= 0; i--)
        {
            var atual = (resto << 32) | a[i];

            quociente[i] = (uint)(atual / divisor);
            resto = atual % divisor;
        }

        return (Magnitude.Aparar(quociente),
                resto == 0 ? Magnitude.Zero : [(uint)resto]);
    }

    private static (uint[], uint[]) AlgoritmoD(uint[] a, uint[] b)
    {
        // 1. Normalizar: deslocar os dois até o divisor ter o bit alto ligado.
        var desvio = System.Numerics.BitOperations.LeadingZeroCount(b[^1]);

        var u = ComEspaco(Magnitude.DeslocarBitsEsquerda(a, desvio), a.Length + 1);
        var v = Magnitude.DeslocarBitsEsquerda(b, desvio);

        var n = v.Length;
        var m = u.Length - n - 1;

        var quociente = new uint[m + 1];

        var vAlto = v[n - 1];
        var vSegundo = v[n - 2];

        for (var j = m; j >= 0; j--)
        {
            // 2. O palpite: os dois dígitos mais altos do resto sobre o mais
            //    alto do divisor.
            var doTopo = ((ulong)u[j + n] << 32) | u[j + n - 1];

            var palpite = doTopo / vAlto;
            var sobra = doTopo % vAlto;

            // 3. A correção de Knuth, ANTES de subtrair. Ela usa o segundo
            //    dígito do divisor para derrubar o palpite quando ele é grande
            //    demais, e evita a maior parte das correções caras depois.
            while (palpite > uint.MaxValue
                   || palpite * vSegundo > ((sobra << 32) | u[j + n - 2]))
            {
                palpite--;
                sobra += vAlto;

                if (sobra > uint.MaxValue)
                {
                    break;
                }
            }

            // 4. Multiplicar e subtrair.
            long empresta = 0;
            ulong vaiUm = 0;

            for (var i = 0; i < n; i++)
            {
                var produto = palpite * v[i] + vaiUm;

                vaiUm = produto >> 32;

                var conta = u[i + j] - (long)(uint)produto - empresta;

                u[i + j] = (uint)conta;
                empresta = conta < 0 ? 1 : 0;
            }

            var ultima = u[j + n] - (long)vaiUm - empresta;

            u[j + n] = (uint)ultima;

            // 5. Se ficou negativo, o palpite era grande demais: devolve o
            //    divisor e desconta um. Knuth provou que isto acontece em menos
            //    de dois por mil dos casos -- e é por ser raro que costuma
            //    passar despercebido.
            if (ultima < 0)
            {
                palpite--;

                ulong devolve = 0;

                for (var i = 0; i < n; i++)
                {
                    var soma = (ulong)u[i + j] + v[i] + devolve;

                    u[i + j] = (uint)soma;
                    devolve = soma >> 32;
                }

                u[j + n] = (uint)(u[j + n] + devolve);
            }

            quociente[j] = (uint)palpite;
        }

        // 6. Desfazer a normalização no resto. O quociente não precisa: ele é
        //    o mesmo, e é por isso que a normalização é legítima.
        var resto = new uint[n];

        Array.Copy(u, resto, n);

        return (Magnitude.Aparar(quociente),
                Magnitude.DeslocarBitsDireita(Magnitude.Aparar(resto), desvio));
    }

    private static uint[] ComEspaco(uint[] palavras, int quantas)
    {
        if (palavras.Length >= quantas)
        {
            var copia = new uint[palavras.Length + 1];

            Array.Copy(palavras, copia, palavras.Length);

            return copia;
        }

        var maior = new uint[quantas];

        Array.Copy(palavras, maior, palavras.Length);

        return maior;
    }
}
