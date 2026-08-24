"""Generates the design document PDF. Run: python Docs/make_design_pdf.py"""
from pathlib import Path

from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER, TA_LEFT
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import mm
from reportlab.platypus import (BaseDocTemplate, Frame, KeepTogether, NextPageTemplate,
                                PageBreak, PageTemplate, Paragraph, Spacer, Table,
                                TableStyle)

OUT = Path(__file__).resolve().parent / "Design_Document_v0.1.pdf"

# ---- the actual game palette, used to style this document -------------------
INK       = colors.HexColor("#17123A")   # studio dark
PINK      = colors.HexColor("#FF3E9D")
CYAN      = colors.HexColor("#00E0FF")
GOLD      = colors.HexColor("#FFC542")
ORANGE    = colors.HexColor("#FF7A2F")
LIME      = colors.HexColor("#9BE84B")
PAPER     = colors.HexColor("#FBF9F4")
GREY      = colors.HexColor("#6C6A85")
RULE      = colors.HexColor("#DDD8CE")

PLAYER_COLOURS = [("P1", "#E8412E"), ("P2", "#2F7BE8"), ("P3", "#FFC542"),
                  ("P4", "#3FB950"), ("P5", "#9B5DE5"), ("P6", "#FF7A2F"),
                  ("P7", "#00E0FF"), ("P8", "#FF3E9D")]

ss = getSampleStyleSheet()
S = {
    "title": ParagraphStyle("t", parent=ss["Title"], fontName="Helvetica-Bold",
                            fontSize=34, leading=38, textColor=INK, alignment=TA_LEFT,
                            spaceAfter=2),
    "sub": ParagraphStyle("s", fontName="Helvetica", fontSize=12.5, leading=17,
                          textColor=GREY, alignment=TA_LEFT, spaceAfter=4),
    "kicker": ParagraphStyle("k", fontName="Helvetica-Bold", fontSize=8.5, leading=12,
                             textColor=PINK, alignment=TA_LEFT, spaceAfter=3),
    "h1": ParagraphStyle("h1", fontName="Helvetica-Bold", fontSize=18, leading=21,
                         textColor=INK, spaceBefore=2, spaceAfter=7),
    "h2": ParagraphStyle("h2", fontName="Helvetica-Bold", fontSize=12.5, leading=16,
                         textColor=INK, spaceBefore=11, spaceAfter=4),
    "h3": ParagraphStyle("h3", fontName="Helvetica-Bold", fontSize=10, leading=13,
                         textColor=PINK, spaceBefore=8, spaceAfter=3),
    "body": ParagraphStyle("b", fontName="Helvetica", fontSize=9.4, leading=13.2,
                           textColor=INK, spaceAfter=5),
    "small": ParagraphStyle("sm", fontName="Helvetica", fontSize=8.4, leading=12,
                            textColor=GREY, spaceAfter=5),
    "quote": ParagraphStyle("q", fontName="Helvetica-Oblique", fontSize=9.8, leading=14.5,
                            textColor=INK, leftIndent=10, rightIndent=8,
                            spaceBefore=4, spaceAfter=7, borderPadding=0),
    "cell": ParagraphStyle("c", fontName="Helvetica", fontSize=8.4, leading=11.0,
                           textColor=INK),
    "cellb": ParagraphStyle("cb", fontName="Helvetica-Bold", fontSize=8.4, leading=11.0,
                            textColor=INK),
    "cellh": ParagraphStyle("ch", fontName="Helvetica-Bold", fontSize=7.6, leading=10,
                            textColor=colors.white),
    "coverbig": ParagraphStyle("cbg", fontName="Helvetica-Bold", fontSize=46, leading=48,
                               textColor=colors.white, alignment=TA_LEFT),
    "coversub": ParagraphStyle("csb", fontName="Helvetica", fontSize=13, leading=19,
                               textColor=colors.HexColor("#C9C4E8"), alignment=TA_LEFT),
    "covertag": ParagraphStyle("ctg", fontName="Helvetica-Bold", fontSize=10, leading=15,
                               textColor=GOLD, alignment=TA_LEFT),
}


def P(t, s="body"):
    return Paragraph(t, S[s])


def rule(c=RULE, h=1.1, space=7):
    t = Table([[""]], colWidths=[170 * mm], rowHeights=[h])
    t.setStyle(TableStyle([("BACKGROUND", (0, 0), (-1, -1), c),
                           ("TOPPADDING", (0, 0), (-1, -1), 0),
                           ("BOTTOMPADDING", (0, 0), (-1, -1), 0)]))
    return KeepTogether([Spacer(1, space), t, Spacer(1, space)])


def table(rows, widths, header=True, headcolour=INK, zebra=True):
    data = []
    for i, r in enumerate(rows):
        style = "cellh" if (header and i == 0) else "cell"
        data.append([Paragraph(str(c), S[style]) for c in r])
    t = Table(data, colWidths=widths, repeatRows=1 if header else 0)
    cmds = [("VALIGN", (0, 0), (-1, -1), "TOP"),
            ("TOPPADDING", (0, 0), (-1, -1), 3.6),
            ("BOTTOMPADDING", (0, 0), (-1, -1), 3.6),
            ("LEFTPADDING", (0, 0), (-1, -1), 7),
            ("RIGHTPADDING", (0, 0), (-1, -1), 7),
            ("LINEBELOW", (0, 0), (-1, -2), 0.4, RULE)]
    if header:
        cmds += [("BACKGROUND", (0, 0), (-1, 0), headcolour),
                 ("LINEBELOW", (0, 0), (-1, 0), 0, colors.white)]
    if zebra:
        for i in range(1 if header else 0, len(data)):
            if i % 2 == (0 if header else 1):
                cmds.append(("BACKGROUND", (0, i), (-1, i), colors.HexColor("#F4F1EA")))
    t.setStyle(TableStyle(cmds))
    return t


def swatches(items, w=21 * mm):
    """A row of colour chips with hex labels underneath."""
    def ink_for(h):
        r, g, b = int(h[1:3], 16), int(h[3:5], 16), int(h[5:7], 16)
        return "#17123A" if (0.299 * r + 0.587 * g + 0.114 * b) > 150 else "white"
    names = [Paragraph(f'<font color="{ink_for(h)}"><b>{n}</b></font>', S["cell"])
             for n, h in items]
    hexes = [Paragraph(f'<font size="6.5" color="#6C6A85">{h}</font>', S["cell"]) for _, h in items]
    t = Table([names, hexes], colWidths=[w] * len(items), rowHeights=[13 * mm, 5 * mm])
    cmds = [("VALIGN", (0, 0), (-1, 0), "BOTTOM"), ("ALIGN", (0, 0), (-1, -1), "CENTRE"),
            ("BOTTOMPADDING", (0, 0), (-1, 0), 4), ("TOPPADDING", (0, 1), (-1, 1), 2)]
    for i, (_, h) in enumerate(items):
        cmds.append(("BACKGROUND", (i, 0), (i, 0), colors.HexColor(h)))
    t.setStyle(TableStyle(cmds))
    return t


def quote(text, accent=GOLD):
    inner = Table([[Paragraph(text, S["quote"])]], colWidths=[164 * mm])
    inner.setStyle(TableStyle([("BACKGROUND", (0, 0), (-1, -1), colors.HexColor("#F4F1EA")),
                               ("LINEBEFORE", (0, 0), (0, -1), 2.4, accent),
                               ("LEFTPADDING", (0, 0), (-1, -1), 9),
                               ("RIGHTPADDING", (0, 0), (-1, -1), 9),
                               ("TOPPADDING", (0, 0), (-1, -1), 7),
                               ("BOTTOMPADDING", (0, 0), (-1, -1), 7)]))
    return KeepTogether([Spacer(1, 2), inner, Spacer(1, 7)])


# ---------------- page furniture ----------------
def cover_page(canv, doc):
    canv.saveState()
    canv.setFillColor(INK)
    canv.rect(0, 0, A4[0], A4[1], stroke=0, fill=1)
    bars = [PINK, ORANGE, GOLD, LIME, CYAN]
    bw = A4[0] / len(bars)
    for i, c in enumerate(bars):
        canv.setFillColor(c)
        canv.rect(i * bw, A4[1] - 13 * mm, bw, 13 * mm, stroke=0, fill=1)
    canv.setFillColor(colors.HexColor("#2A2158"))
    for i in range(9):
        canv.circle(28 * mm + i * 19 * mm, 52 * mm, 5.4 * mm, stroke=0, fill=1)
    canv.setFillColor(GOLD)
    for i in range(9):
        if i % 2 == 0:
            canv.circle(28 * mm + i * 19 * mm, 52 * mm, 5.4 * mm, stroke=0, fill=1)
    canv.restoreState()


def inner_page(canv, doc):
    canv.saveState()
    canv.setFillColor(PAPER)
    canv.rect(0, 0, A4[0], A4[1], stroke=0, fill=1)
    canv.setFillColor(INK)
    canv.rect(0, A4[1] - 6 * mm, A4[0], 6 * mm, stroke=0, fill=1)
    seg = A4[0] / 5
    for i, c in enumerate([PINK, ORANGE, GOLD, LIME, CYAN]):
        canv.setFillColor(c)
        canv.rect(i * seg, A4[1] - 6 * mm, seg, 1.8 * mm, stroke=0, fill=1)
    canv.setFont("Helvetica", 7.4)
    canv.setFillColor(GREY)
    canv.drawString(20 * mm, 11 * mm, "Untitled Party Game  -  Design Document v0.1  -  Confidential")
    canv.drawRightString(A4[0] - 20 * mm, 11 * mm, str(canv.getPageNumber()))
    canv.restoreState()


doc = BaseDocTemplate(str(OUT), pagesize=A4,
                      leftMargin=20 * mm, rightMargin=20 * mm,
                      topMargin=18 * mm, bottomMargin=18 * mm,
                      title="Untitled Party Game - Design Document v0.1",
                      author="the publisher")
fw = A4[0] - 40 * mm
doc.addPageTemplates([
    PageTemplate(id="cover", frames=[Frame(20 * mm, 60 * mm, fw, 150 * mm, id="cf",
                                           leftPadding=0, rightPadding=0,
                                           topPadding=0, bottomPadding=0)],
                 onPage=cover_page),
    PageTemplate(id="inner", frames=[Frame(20 * mm, 18 * mm, fw, A4[1] - 40 * mm, id="nf",
                                           leftPadding=0, rightPadding=0,
                                           topPadding=0, bottomPadding=0)],
                 onPage=inner_page),
])

E = []
A = E.append

# ================= COVER =================
A(Spacer(1, 34 * mm))
A(Paragraph("UNTITLED", S["coverbig"]))
A(Paragraph('<font color="#FF3E9D">PARTY GAME</font>', S["coverbig"]))
A(Spacer(1, 8))
A(Paragraph("Design Document  -  version 0.1", S["coversub"]))
A(Spacer(1, 16))
A(Paragraph("An online party game show for 2-8 players, hosted by someone<br/>"
            "who watched every humiliating thing you did tonight.", S["coversub"]))
A(Spacer(1, 20))
A(Paragraph("STEAM  /  WINDOWS FIRST  /  ONLINE, KEYBOARD-FIRST", S["covertag"]))
A(Spacer(1, 6))
A(Paragraph("the publisher  -  August 2026", S["coversub"]))
A(NextPageTemplate("inner"))
A(PageBreak())

# ================= 1. THE GAME =================
A(P("01", "kicker"))
A(P("The game", "h1"))
A(P("You and up to seven friends - each on your own PC, wherever you are - are contestants on a chaotic televised game show. "
    "You play short, silly challenges. Between them you move around a studio floor "
    "where traps, swaps and shady offers await. Presiding over all of it is a host "
    "who genuinely watched everything you did and will not let you forget it."))
A(P("By round five he is running jokes about your friends that he invented himself. "
    "In the finale he pays them off. Every group's session becomes its own story, and "
    "that story is different every time because it is built from what actually happened."))
A(rule())
A(table([
    ["", ""],
    ["Players", "2-8, online. Everyone plays from their own PC, on their own screen."],
    ["Session length", "15 / 30 / 45 minutes, chosen at the start"],
    ["Round length", "30-60 seconds per minigame"],
    ["Platform", "Steam, Windows primary"],
    ["Networking", "Steam P2P (Lobbies + SteamNetworkingSockets). Host-authoritative. "
                    "Free relay and friend invites - no servers to run, no hosting bill."],
    ["Engine", "Unity 6 LTS"],
    ["Input", "<b>Keyboard first</b>, gamepad fully supported. Two action buttons maximum."],
], [38 * mm, fw - 38 * mm], header=False, zebra=True))
A(Spacer(1, 8))
A(P("Design principles", "h2"))
A(table([
    ["Principle", "What it means in practice"],
    ["<b>Playable by all</b>",
     "Two buttons. No tutorial. A parent, a child and a drunk friend can all join round one "
     "and be competitive by round two."],
    ["<b>The loser matters more than the winner</b>",
     "Every minigame needs a clear, funny, recognisable way to fail. The failure is what the "
     "host makes jokes about, and the jokes are the product."],
    ["<b>Instantly readable</b>",
     "Everyone has their own screen, but shared arenas still hold up to eight characters. "
     "You must know who just died without reading a name."],
    ["<b>The game survives without the AI</b>",
     "Remove the host and this is still a working party game. The AI adds identity, not "
     "function. This is deliberate and non-negotiable."],
    ["<b>Fun is discovered, not assumed</b>",
     "Twelve minigames are designed; roughly four are expected to be cut after playtesting. "
     "That is the plan, not a failure."],
], [44 * mm, fw - 44 * mm]))
A(PageBreak())

# ================= 2. LOOK AND FEEL =================
A(P("02", "kicker"))
A(P("Look and feel", "h1"))
A(P("<b>The direction in one line: Saturday night television, 1979, on far too much sugar.</b>"))
A(P("Everything is a garish TV studio. Chrome, chase lights, an audience in silhouette, "
    "confetti cannons that fire at the slightest excuse, and a set that is trying much harder "
    "than the budget allowed. The aesthetic is doing three jobs at once: it gives the game a "
    "distinctive identity, it justifies extremely saturated colours which is what makes eight "
    "players readable on one screen, and it is funny before anything even happens."))
A(P("Practically, it is also cheap. One studio environment is reused for the entire game, "
    "redressed per minigame. There is no world to build."))

A(P("Palette", "h2"))
A(P("Deep studio dark as the ground, with hot broadcast colours on top. High contrast "
    "everywhere - this is a game watched from a sofa, not studied up close.", "small"))
A(swatches([("STUDIO", "#17123A"), ("HOT PINK", "#FF3E9D"), ("CYAN", "#00E0FF"),
            ("GOLD", "#FFC542"), ("ORANGE", "#FF7A2F"), ("LIME", "#9BE84B"),
            ("PAPER", "#FBF9F4")], w=(fw / 7)))
A(Spacer(1, 9))
A(P("Player colours", "h2"))
A(P("One saturated colour per player, applied to the whole body. These are chosen to stay "
    "distinct at small size, in motion, and against the desaturated arenas.", "small"))
A(swatches(PLAYER_COLOURS, w=(fw / 8)))
A(Spacer(1, 9))

A(P("Characters", "h2"))
A(P("Chunky, simple, slightly wobbly. Deliberately crude silhouettes - closer to a bath toy "
    "than a person. Big eyes, almost no face. Physics-driven wobble does the acting, which "
    "means comedy comes from the simulation rather than from animation work."))
A(table([
    ["Rule", "Why"],
    ["One bold colour per player, whole body",
     "The single most important readability decision. You should know who died without reading a name."],
    ["A distinct silhouette prop each (hat, antenna, cone, halo)",
     "Colour alone fails for colourblind players and in a scrum. Shape is the backup channel."],
    ["Name tag floating above, always on",
     "Non-negotiable with eight players. Your own character also gets a highlight ring, "
     "since each player has their own view."],
    ["Arenas desaturated, players saturated",
     "Characters must pop off the background at all times."],
    ["Camera always frames every living player",
     "Rendered per client, so it can also favour YOUR character slightly. If someone "
     "cannot see themselves, they are not playing."],
], [56 * mm, fw - 56 * mm]))
A(PageBreak())

A(P("The set", "h2"))
A(P("A single television studio, redressed per challenge. Raised stage, chase lights around "
    "the edge, chrome trim, a giant scoreboard, and an audience rendered as silhouettes "
    "that react - standing up, throwing things, going silent at the right moment."))
A(P("<b>The host appears on a giant studio screen above the stage.</b> This is a deliberate "
    "production decision as much as an artistic one: a screen is far cheaper than a fully "
    "animated character, it is completely diegetic for a game show, and it means his "
    "presence can be upgraded later - a face, then a body, then a character who walks onto "
    "the set - without redesigning anything."))

A(P("Interface", "h2"))
A(P("Television broadcast language, not video game language. Lower thirds, score bugs, big "
    "chunky numbers that slam onto screen, wipe transitions between rounds. The UI should "
    "look like it is being cut live by a director who is slightly panicking."))
A(table([
    ["Element", "Treatment"],
    ["Scores", "Broadcast score bug, bottom of screen, always visible, player colours"],
    ["Round titles", "Full-screen slam card with the challenge name before each minigame"],
    ["Host lines", "Lower third under the studio screen, with the line also spoken aloud"],
    ["Eliminations", "Instant, loud, and funny - a stamp, a buzzer, confetti of the wrong colour"],
    ["Board", "Seen from above like a studio floor plan, pieces are the characters themselves"],
], [40 * mm, fw - 40 * mm]))

A(P("Audio", "h2"))
A(P("Audio carries more of a party game than art does, and costs a fraction as much. A brass "
    "sting for wins, a descending trombone for failures, a live studio audience that gasps "
    "and jeers, and cheap chase music that never quite stops. The host is voiced - a read "
    "host is a fraction as good as a heard one, and delivery is most of the comedy."))
A(quote("Open question, deliberately unresolved: whether the game is 3D or 2D. Everything "
        "above works in either. A 2D or 2.5D treatment would cut art cost enormously, "
        "iterate far faster, and run on almost any machine - and Pico Park, Duck Game and "
        "Ultimate Chicken Horse are proof that it costs nothing that matters.", CYAN))
A(PageBreak())

# ================= 3. HOW A SESSION FLOWS =================
A(P("03", "kicker"))
A(P("How a session flows", "h1"))
A(table([
    ["#", "Beat", "Time", "What happens"],
    ["1", "<b>Sign in</b>", "2 min",
     "Everyone picks a colour and types a name. The host uses these names constantly, so "
     "this is the only setup that matters."],
    ["2", "<b>Host intro</b>", "15 sec",
     "The challenge is announced, framed by where everyone currently stands."],
    ["3", "<b>Minigame</b>", "30-60 sec", "Everyone plays. Clear winner, clearer loser."],
    ["4", "<b>Host reaction</b>", "10 sec",
     "The moment the game exists for. He names what actually happened and ties it to the "
     "night's running jokes."],
    ["5", "<b>Board move</b>", "20 sec",
     "Placement decides how far you move. Landing somewhere triggers something."],
    ["6", "", "", "Repeat 2-5 for roughly eight rounds."],
    ["7", "<b>The finale</b>", "2 min",
     "Double points and a comeback mechanic, so nobody is mathematically dead early."],
    ["8", "<b>Wrap-up</b>", "1 min",
     "The host crowns a winner and hands out titles he invented himself. This is the part "
     "people screenshot."],
], [8 * mm, 30 * mm, 16 * mm, fw - 54 * mm]))

A(P("The board - the studio floor", "h2"))
A(P("A small loop, quick to move around, dense with interaction. Its job is not to be a "
    "board game; its job is to create sabotage, comebacks and grudges between minigames."))
A(table([
    ["Space", "Effect"],
    ["<b>Prize</b>", "Points."],
    ["<b>Trap</b>", "Lose points, or carry a handicap into the next challenge."],
    ["<b>Swap</b>", "Trade board positions with any player. Creates instant enemies."],
    ["<b>Sabotage</b>", "Place a trap on the board for somebody else to find later."],
    ["<b>Audience Vote</b>", "Everyone votes. The winner of the vote is punished."],
    ["<b>The Host's Office</b>",
     "The host offers you a personal deal, built from your actual night. This is the space "
     "where the AI stops being decoration."],
], [36 * mm, fw - 36 * mm]))
A(PageBreak())

# ================= 4. THE HOST =================
A(P("04", "kicker"))
A(P("The host", "h1"))
A(P("He is the reason this game is not interchangeable with any other party game. He receives "
    "the real state of the show - standings, what just happened, the full history of the night "
    "- and speaks two sentences. He remembers everything."))
A(P("The examples below are unedited output from the working prototype, not concept writing.", "small"))
A(P("Round one", "h3"))
A(quote("Ravi, the zen master of Plank Panic! Who knew standing still was the winning strategy?", PINK))
A(P("Round three - a pattern forms on its own", "h3"))
A(quote("Priya, you're redefining the term 'aquatics event' this evening! Round three was "
        "meant to be about buckets, not synchronised swimming.", PINK))
A(P("The Host's Office - a deal built from her actual night", "h3"))
A(quote("Priya, my frequent moat diver, I've got a deal sweeter than the cake you never "
        "reached. How about triple points - if you can avoid water-based humiliation in the "
        "next round? Do we have a splash-free agreement?", GOLD))
A(P("That offer references two separate failures and a third unrelated one, proposes a "
    "concrete trade with real risk, and asks her to accept. No scripted game can make it, "
    "because no scripted game knows her night went that way."))

A(P("Two rules that make it work", "h2"))
A(table([
    ["Rule", "Reason"],
    ["<b>Two sentences maximum</b>",
     "A host who rambles kills the pace of a party. Brevity is enforced in the prompt and "
     "tested."],
    ["<b>Lines are generated during the previous minigame</b>",
     "The outcome of a round is knowable a beat before it ends. Waiting five seconds for a "
     "response while four people stare at a screen is fatal, so the line is always ready "
     "before it is needed. This is built and not optional."],
], [52 * mm, fw - 52 * mm]))

A(P("Where he goes next", "h2"))
A(P("The host is designed to be promoted over time, on top of a game that already works "
    "without him."))
A(table([
    ["Version", "The host is...", "Risk if it fails"],
    ["<b>1.0</b>", "A narrator with memory. Pure flavour.", "None - cut him, the game still ships"],
    ["<b>1.x</b>", "Running show events; can favour or punish players", "Low"],
    ["<b>2.0</b>", "Bargainable - argue, flatter or bribe him for advantage", "Medium"],
    ["<b>3.0</b>", "A genuine opponent and character in his own right", "The long-term ambition"],
], [20 * mm, 62 * mm, fw - 82 * mm]))
A(PageBreak())

# ================= 5. MINIGAMES =================
A(P("05", "kicker"))
A(P("The twelve minigames", "h1"))
A(P("Twelve are designed; roughly eight are expected to ship. They are grouped into five "
    "families that share a character controller, arena and camera - so the fourth game in a "
    "family costs a fraction of the first. That is what makes twelve achievable, and it is "
    "why cutting a weak one costs almost nothing."))
A(P("Every entry lists its <b>signature humiliation</b>: the specific, recognisable way you "
    "fail. That is what the host builds running jokes from.", "small"))

fam = [
    ("A. ARENA", "shared: physics character, platform, shove", LIME, [
        ("1. Plank Panic", "Move + Shove",
         "A narrow plank over a long drop. Last one standing wins.",
         "Falling off in the first two seconds, having touched nobody."),
        ("2. Sumo Circle", "Move + Charge",
         "The platform shrinks every ten seconds. Barge people off.",
         "Knocking yourself off with your own charge."),
        ("3. Vanishing Floor", "Move + Jump",
         "Tiles drop away in waves. Be standing on something.",
         "Sprinting confidently onto the exact tile about to go."),
        ("4. The Rising Room", "Move + Jump",
         "The floor rises toward a spiked ceiling. Climb over each other.",
         "Being used as a step-ladder by a friend."),
    ]),
    ("B. DODGE", "shared: overhead spawner, falling objects", CYAN, [
        ("5. Bucket Roulette", "Move only",
         "Buckets rain down. Most are harmless. Some are not.",
         "Standing perfectly still in the one fatal spot."),
        ("6. Sticky Fingers", "Move + Grab",
         "Grab falling prizes for points. Some are bombs. Greed is punished.",
         "Catching three bombs in a row while everyone else scores."),
    ]),
    ("C. RACE", "shared: track, obstacles, bump", ORANGE, [
        ("7. Cake Sprint", "Move + Balance",
         "Carry a wobbling cake to the finish. Any bump and it goes everywhere.",
         "Dropping it on the finish line."),
        ("8. Greased Ladder", "Move + Grab",
         "Climb a slippery ladder. Others can grab your ankles.",
         "Being dragged from first to last in the final second."),
    ]),
    ("D. HOST-DRIVEN", "minimal art, maximum host - build these first", GOLD, [
        ("9. Red Light, Barnaby", "Move only",
         "The host calls GO and STOP. He is biased, and he lies. He will let a favourite "
         "creep forward and call you out unfairly, because he remembers who annoyed him.",
         "Being eliminated by a host who is openly cheating against you."),
        ("10. Say What He Says", "Two buttons",
         "The host issues escalating silly instructions, generated live and referencing your "
         "night. Perform them in order.",
         "Confidently performing the sequence from two rounds ago."),
        ("11. The Prediction", "Menu select",
         "Everyone secretly bets on who will come last in the NEXT minigame. Creates table "
         "talk, alliances and betrayal between rounds.",
         "Everyone unanimously betting against one person, who then wins."),
    ]),
    ("E. CHAOS", "shared: passable object, hidden timer", PINK, [
        ("12. Live Grenade", "Move + Throw",
         "Pass the parcel with a timer nobody can see.",
         "Accepting it with two seconds left because somebody lied to you."),
    ]),
]

for name, note, col, games in fam:
    A(Spacer(1, 5))
    hdr = Table([[Paragraph(f'<font color="white"><b>{name}</b></font>', S["cell"]),
                  Paragraph(f'<font color="white" size="7.4">{note}</font>', S["cell"])]],
                colWidths=[42 * mm, fw - 42 * mm])
    hdr.setStyle(TableStyle([("BACKGROUND", (0, 0), (-1, -1), INK),
                             ("LINEBEFORE", (0, 0), (0, -1), 3, col),
                             ("TOPPADDING", (0, 0), (-1, -1), 5),
                             ("BOTTOMPADDING", (0, 0), (-1, -1), 5),
                             ("LEFTPADDING", (0, 0), (-1, -1), 8),
                             ("VALIGN", (0, 0), (-1, -1), "MIDDLE")]))
    A(hdr)
    block = None
    rows = [["Game", "Controls", "Rule", "Signature humiliation"]]
    for g, ctrl, rule_, sig in games:
        rows.append([f"<b>{g}</b>", ctrl, rule_, f"<i>{sig}</i>"])
    A(table(rows, [33 * mm, 20 * mm, 58 * mm, fw - 111 * mm],
            headcolour=colors.HexColor("#3A3168")))

A(Spacer(1, 6))
A(quote("Build order: Family D first. Those three need almost no art, they prove the host as "
        "a mechanic rather than decoration, and if the host is not fun there we learn it for "
        "almost nothing. Then Family A, which unlocks four games from one controller and arena.",
        GOLD))
A(PageBreak())

# ================= 6. CONTROLS =================
A(P("06", "kicker"))
A(P("Controls", "h1"))
A(P("<b>Keyboard first.</b> Most people on Steam play with a keyboard, and everyone is on "
    "their own machine, so keyboard is the primary input and gamepad is a fully supported "
    "alternative - not the other way round."))
A(P("<b>Two action buttons. That is the entire scheme, and it is a hard rule.</b> Every "
    "minigame maps onto the same handful of inputs, so nobody is ever taught anything twice "
    "and a player joining at round four is not at a disadvantage."))
A(table([
    ["Action", "Keyboard", "Gamepad", "Notes"],
    ["<b>Move</b>", "WASD or Arrows", "Left stick / D-pad",
     "The only movement input. No sprint, no crouch, no camera control."],
    ["<b>Primary</b>", "Space", "A",
     "Context-sensitive: jump, shove, grab, throw. Always the 'do the thing' key."],
    ["<b>Secondary</b>", "Shift or E", "B",
     "Used in roughly half the minigames: charge, balance, drop. Never required to win."],
    ["<b>Menu / vote</b>", "1-4 or Arrows + Enter", "Face buttons",
     "Board choices, deals, and The Prediction."],
    ["<b>Pause</b>", "Esc", "Start", "Votes to pause, since nobody is in the same room."],
], [24 * mm, 34 * mm, 30 * mm, fw - 88 * mm]))
A(Spacer(1, 5))
A(P("Both schemes are rebindable, and the game never mixes them - you pick one at the "
    "lobby and the on-screen prompts match what you are holding.", "small"))
A(Spacer(1, 4))
A(table([
    ["Rule", "Reason"],
    ["<b>No combos, no holds, no timing windows tighter than about a third of a second</b>",
     "Anyone should be able to join and compete immediately. Precision is the enemy of a "
     "party game, and it is also the first thing that breaks over a network."],
    ["<b>Every minigame is playable with movement alone at a basic level</b>",
     "Buttons add expression, never the ability to participate."],
    ["<b>Two hands, no chords</b>",
     "One hand on movement, one on the action keys. Nothing requires reaching across the "
     "keyboard."],
    ["<b>Drop-in and drop-out between rounds</b>",
     "People arrive late to parties. The host will absolutely comment on it."],
], [58 * mm, fw - 58 * mm]))

A(P("How a game is joined", "h2"))
A(P("Friends-first, deliberately. This is a game you play with people you know, so the flow "
    "is built around invites rather than public matchmaking - which also avoids the "
    "empty-lobby problem that kills small online games."))
A(table([
    ["Step", "What happens"],
    ["<b>1. One player opens a show</b>", "Creates a Steam lobby. They become the host machine."],
    ["<b>2. Invite</b>", "Steam friend invite, or a short join code for people not on your friends list."],
    ["<b>3. Lobby</b>",
     "Pick colour and name, choose keyboard or gamepad, see who else is in. The host picks "
     "show length. The AI host warms up the crowd while you wait."],
    ["<b>4. Play</b>", "2-8 players. Late joiners can drop in between rounds."],
], [42 * mm, fw - 42 * mm]))
A(PageBreak())

A(P("Modes", "h2"))
A(table([
    ["Mode", "How it plays"],
    ["<b>Party</b>", "3-8 players online, rotating through the board and challenges, eight rounds, highest total wins."],
    ["<b>Teams</b>", "Two teams. Voice chat naturally splits the room into conspiracies, which is the point."],
    ["<b>Solo / Career</b>",
     "Climb the show's ladder against AI contestants, with the host taking a progressively "
     "greater interest in you as you win. This is also where he can later become a real "
     "character - first favouring you, then resenting you."],
], [30 * mm, fw - 30 * mm]))
A(PageBreak())

# ================= 7. PLATFORM =================
A(P("07", "kicker"))
A(P("Platform, technology and open questions", "h1"))
A(table([
    ["Item", "Decision"],
    ["Engine", "Unity 6 LTS"],
    ["Target", "Steam, Windows primary"],
    ["Networking",
     "<b>Steam P2P.</b> Steam Lobbies for invites and discovery, SteamNetworkingSockets for "
     "transport. Valve provides NAT punching and relay <b>free</b>, so there are no servers "
     "to run and no hosting bill. Likely stack: Mirror or Unity Netcode for GameObjects over "
     "a Steam transport."],
    ["Netcode model",
     "<b>Host-authoritative.</b> One player's machine is the truth; everyone else sends "
     "inputs and receives state. Short rounds and forgiving physics make this the easy case "
     "- and a hiccup in a party game is funny rather than fatal."],
    ["Player count", "2-8. Designed around friend invites, not public matchmaking."],
    ["Development machine",
     "Editor work on a MacBook; Windows builds tested on the Windows laptop, which is the "
     "actual Steam target."],
    ["AI service",
     "The host runs against a language model through a local service. The API key never "
     "ships inside the game binary."],
    ["Steam AI disclosure",
     "<b>Mandatory and will be complied with.</b> Under Valve's January 2026 rules the host "
     "is 'live-generated content' and must be declared. The store page sells the party game; "
     "the disclosure field states the truth plainly. Both are honest."],
], [40 * mm, fw - 40 * mm]))

A(P("Deliberately open", "h2"))
A(table([
    ["Question", "Position"],
    ["<b>2D or 3D?</b>",
     "Genuinely undecided. The 3D requirement is inherited from an earlier, abandoned "
     "concept and may not deserve to survive. 2D would cut art cost enormously, iterate far "
     "faster and run anywhere."],
    ["<b>The name</b>",
     "Not chosen. The trademark and collision check happens BEFORE any art is commissioned."],
    ["<b>The host's voice</b>",
     "Text-to-speech provider undecided. This is the one place worth spending AI budget - "
     "delivery is most of the comedy."],
    ["<b>The board's exact shape</b>", "Loop, ladder or bracket. To be resolved in prototyping."],
    ["<b>Which four minigames get cut</b>", "Answered by playtesting, not by argument."],
], [40 * mm, fw - 40 * mm]))

A(Spacer(1, 4))
A(quote("Design correction, August 2026: an earlier draft planned same-screen local play "
        "with Steam Remote Play Together supplying the online. That was wrong. Remote Play "
        "Together merges every keyboard into a single input stream, so it supports exactly "
        "ONE keyboard player - everyone else needs a gamepad. Since most people on Steam "
        "play with a keyboard and nobody is sitting in the same room, the game is online "
        "from the start and keyboard-first. Every reference title - Fall Guys, Gang Beasts, "
        "Pummel Party, Pico Park - is online for the same reason.", PINK))

A(rule())
A(P("What is already proven", "h2"))
A(P("The host works. It is built, running, and the lines quoted in section 04 are real output "
    "rather than aspiration. Everything else in this document is a party game constructed "
    "around it - and, deliberately, one that still works if the host were removed entirely."))
A(Spacer(1, 10))
A(P("the publisher  -  design document v0.1  -  August 2026  -  "
    "not for distribution", "small"))

doc.build(E)
print(f"written: {OUT}")
