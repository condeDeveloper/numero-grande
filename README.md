# numero-grande

Inteiros de precisão arbitrária do zero em C# e .NET 8 — soma, subtração,
multiplicação por Karatsuba, divisão longa pelo algoritmo D de Knuth, potência
modular e leitura e escrita em qualquer base de 2 a 36.

```csharp
var fatorial = Numero.Um;

for (var i = 1; i <= 1000; i++)
{
    fatorial *= Numero.De(i);
}

fatorial.ToString().Length    // 2568 dígitos, todos certos
```

## O juiz

O `System.Numerics.BigInteger` do próprio .NET. É o melhor juiz que um projeto
destes pode ter, por duas razões que não se encontram juntas com frequência.

**Ele é exato.** Não há tolerância, não há arredondamento, não há "próximo o
bastante": ou os dois números são o mesmo ou não são. Num projeto de ponto
flutuante a comparação começa com uma discussão sobre epsilon; aqui não há
discussão.

**Ele é ilimitado.** Dá para gerar milhões de casos de qualquer tamanho e
comparar todos. Não há tabela de vetores para acabar, não há corpus para baixar:
o juiz responde a qualquer pergunta que se faça.

| o que se compara | quantos |
|---|---|
| somas e subtrações | **100.000** |
| multiplicações | **100.000** |
| divisões, com quociente **e** resto | **100.000** |
| divisões com divisor de dezenas de palavras | 20.000 |
| comparações e ordenação | 50.000 |
| potência, deslocamento e mdc | 3.000 cada |
| potência modular | 2.000 |
| escrever e ler em **todas** as bases de 2 a 36 | 5.000 × 35 |

E o que se prova é forte de verdade: uma aritmética com defeito **não estoura**.
Ela devolve um número — errado num dígito do meio, que passa por qualquer
conferência que uma pessoa faria a olho.

## A constante que eu copiei e que estava errada

Toda biblioteca de números grandes tem um limite: a partir de que tamanho vale a
pena usar Karatsuba em vez da conta de escola. O OpenJDK usa 32 palavras. Copiei.

Aí escrevi a ferramenta de medida, que varre os limites possíveis e mostra qual
dá o menor tempo em cada tamanho:

```
$ dotnet medidor.dll limite

  palavras         8        16        32        64       128       256    escola   melhor
------------------------------------------------------------------------------------------
       512     0.464     0.261     0.149     0.150     0.126     0.142     0.202   128
      1024     1.407     0.775     0.618     0.463     0.440     0.461     0.997   128
      2048     4.323     2.738     1.673     1.497     1.319     1.489     3.478   128
```

O valor copiado deixava a multiplicação **40% mais lenta** do que ela podia ser.

O motivo é que o limite certo depende de quanto custa *montar* um nível de
recursão, e isso depende da linguagem, do coletor de lixo e da implementação —
esta aloca mais por nível que a do OpenJDK, então precisa de pedaços maiores
para compensar. **Copiar a constante de outra biblioteca é copiar a máquina de
outra pessoa.**

Com o valor medido, Karatsuba fica **2,3 vezes** mais rápido que a conta de
escola em números de 32 mil bits, e 2,6 em 65 mil.

## A medida que eu tirei dos testes

A primeira versão tinha um teste comparando o limite medido com o copiado. Ele
falhava de forma intermitente: sob o executor de testes, com várias classes em
paralelo, as duas medidas davam **10,0 ms e 9,8 ms** — uma diferença de ruído. A
mesma comparação, na ferramenta, dá 1,3 ms contra 1,7 ms de forma estável.

A conclusão não é que a medida estava errada: é que **o lugar dela não é lá**. Um
teste que falha sem que nada tenha piorado ensina a ignorar testes, que é o pior
que pode acontecer com uma bateria. Ficaram nos testes só as afirmações de
**formato** — Karatsuba ganha, dobrar o tamanho não quadruplica o tempo — com
margens folgadas de propósito.

## As três decisões de projeto

**A base é 2³²**, não 2⁶⁴ nem 10⁹. A soma de dois dígitos com o "vai um" precisa
caber num tipo que a máquina some de uma vez, e o produto de dois dígitos precisa
caber em **dois**. Com base 2³² os dois cabem num `ulong` e o processador faz a
conta numa instrução; com 2⁶⁴ o produto precisaria de 128 bits. Uma base decimal
tornaria a impressão trivial e a aritmética duas vezes mais lenta — e imprimir
acontece uma vez, somar acontece milhões.

**Sinal separado da magnitude**, e não complemento de dois — o oposto do que o
`BigInteger` do .NET faz. Com complemento de dois a soma é sempre a mesma conta,
e a magnitude de um negativo nunca é o que está no vetor. Para uma biblioteca que
quer ser lida, sinal e magnitude ganha: o vetor guarda o que ele parece guardar.

**Zero não tem sinal.** O construtor faz a regra valer sempre. Sem ela existiriam
dois zeros, e `a == b` deixaria de valer para eles — o defeito mais chato de uma
aritmética de sinal e magnitude. Há um teste que monta o zero de nove jeitos
diferentes e exige que os nove sejam o mesmo objeto lógico.

## A divisão é a única operação difícil

Somar é um laço. Multiplicar é dois laços. **Dividir** é o algoritmo que Knuth
gastou dez páginas descrevendo no volume 2 do *The Art of Computer Programming*,
com duas correções que quase ninguém acerta de primeira.

O problema é o palpite. A divisão longa funciona adivinhando um dígito do
quociente de cada vez, e na base 10 a gente adivinha olhando: "cabe umas três
vezes". Na base 2³² não dá para olhar.

A saída de Knuth é dividir os **dois** dígitos mais altos do que resta pelo
dígito mais alto do divisor, e ele provou que esse palpite erra no máximo **dois
para cima** e nunca para baixo — desde que o divisor esteja normalizado, com o
bit mais alto ligado. Daí os dois passos que parecem sobra e não são:

1. **A normalização.** Os dois números são deslocados até o divisor ter o bit
   mais alto ligado. Isso não muda o quociente — muda só o resto, desfeito no
   fim — e é o que faz a garantia valer.
2. **A correção.** Depois de subtrair, se o resultado ficou negativo, o palpite
   era grande demais: devolve-se o divisor e desconta-se um. Acontece em **menos
   de dois por mil** dos casos — e é exatamente por ser raro que costuma passar
   despercebida. O código funciona em todos os testes escritos à mão e erra num
   número de mil dígitos.

Cem mil divisões sorteadas dão umas duzentas chances de essa correção estar
errada e aparecer.

## Karatsuba, em três linhas

Partindo cada número em duas metades:

```
a·b = a₁b₁·B² + (a₁b₀ + a₀b₁)·B + a₀b₀
```

São quatro multiplicações. Karatsuba percebeu, em 1960, que o termo do meio sai
dos outros dois com **uma** multiplicação a mais em vez de duas:

```
a₁b₀ + a₀b₁ = (a₁ + a₀)(b₁ + b₀) − a₁b₁ − a₀b₀
```

Três multiplicações de metade do tamanho em vez de quatro, e o custo cai de n²
para n^1,585.

A história é boa: Kolmogorov havia conjecturado, num seminário de 1960, que n²
era o mínimo. Karatsuba, com 23 anos, o refutou em uma semana. Kolmogorov
publicou o resultado em nome dele e encerrou o seminário.

## Contra o .NET

```
$ dotnet medidor.dll contra

operação                 dígitos         meu        .NET     razão
------------------------------------------------------------------
multiplicar                 1000    0.025 ms    0.021 ms      1.2x
multiplicar                10000    1.320 ms    0.645 ms      2.0x
escrever em decimal        10000    6.154 ms    2.023 ms      3.0x
```

Não é para ganhar: o `BigInteger` do .NET tem anos de otimização e caminhos em
código nativo. É para **saber a distância** — e uma razão de duas ou três vezes
quer dizer que o algoritmo está certo e falta o acabamento. Vinte vezes quereria
dizer outra coisa.

A operação mais cara é escrever em decimal, e o motivo é que 10 não é potência
de 2: cada dígito exige uma divisão do número inteiro. O truque que reduz isso em
nove vezes — dividir por 10⁹ de uma vez, a maior potência de 10 que cabe numa
palavra — está lá, e ainda assim é a conta mais lenta que a biblioteca tem.

## Rodar

.NET 8. Zero dependências fora do xUnit, e só nos testes.

```
dotnet test testes/NumeroGrande.Testes/NumeroGrande.Testes.csproj -c Release
dotnet run --project ferramentas/Medidor/Medidor.csproj -c Release -- limite
```

## O que ele não faz

Não tem Toom-Cook nem FFT, que é o que uma biblioteca séria usa acima de umas
dezenas de milhares de dígitos. Não tem raiz quadrada, nem inverso modular, nem
teste de primalidade — a potência modular está lá porque é o coração do RSA, e o
resto que o RSA precisa, não. Não tem operações bit a bit (`&`, `|`, `^`), que
num número com sinal e magnitude exigem decidir o que significam em negativos. E
não é um tipo `struct` sem alocação: cada operação devolve um vetor novo.

## Licença

MIT.
