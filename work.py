import numpy as np
import matplotlib.pyplot as plt
from pathlib import Path

fig, ax = plt.subplots(figsize=(8, 8), dpi=150)
fig.patch.set_facecolor("#0b1026"); ax.set_facecolor("#0b1026")


def ring(r, k, s, off):
    p = [(r * np.cos(a), r * np.sin(a))
         for a in np.linspace(off, off + 2 * np.pi, k, endpoint=False)]
    for i in range(k):
        (x1, y1), (x2, y2) = p[i], p[(i + 1) % k]
        ax.plot([x1, x2], [y1, y2], c="#7ec8ff", alpha=.45, lw=1)
    for x, y in p:
        ax.scatter(x, y, s=s, c="#ffe3b0", zorder=3)
    return p


def fork(a, b):
    for i in range(len(a)):
        for j in (2 * i, 2 * i + 1):
            ax.plot([a[i][0], b[j][0]], [a[i][1], b[j][1]],
                    c="#7ec8ff", alpha=.35, lw=.9)


r1 = ring(2, 4, 110, np.pi / 4)
r2 = ring(4, 8, 70, np.pi / 8)
r3 = ring(6.5, 16, 45, np.pi / 16)
fork(r1, r2)
fork(r2, r3)
ax.scatter(0, 0, s=1200, c="#ffe3b0", alpha=.1, zorder=2)

ax.set_xlim(-8, 8); ax.set_ylim(-8, 8)
ax.set_xticks([]); ax.set_yticks([])
[sp.set_visible(False) for sp in ax.spines.values()]

out = Path(__file__).parent
fig.savefig(out / "my_mind_starweb.png", facecolor="#0b1026", bbox_inches="tight")

(out / "statement.txt").write_text(
    "如果要把自己的智力比作一样东西，我愿意把它比作一片有秩序的星网。"
    "每一颗星是一个知识点，它们并非杂乱地散落，而是沿着层层轨道排列："
    "内圈是根基性的核心知识，外圈是不断新学的领域，一圈比一圈辽阔。"
    "而思考就是星与星之间的连线——同轨道上的知识首尾呼应形成体系，"
    "每颗内圈的星都向外分出两条连线，连向更外圈的两颗新星，"
    "知识就这样一层层以倍增的方式生长、扩张。"
    "它不像一座已经完工、只等检索的图书馆，更像一片仍在被点亮的星空："
    "每弄懂一个新问题，就有一颗新星入轨，并与已有的星连成新的星座。"
    "这片星网也会有云雾飘过：疲惫、遗忘、偏见都会暂时遮住一些光；"
    "但只要保持好奇、不断学习，轨道就会一圈圈向外生长，"
    "整片网络能照亮的范围也会越来越大。"
    "所以在我看来，智力不是一个固定的分数，而是一张有秩序、会生长的网。",
    encoding="utf-8")
print("已保存: my_mind_starweb.png 和 statement.txt")