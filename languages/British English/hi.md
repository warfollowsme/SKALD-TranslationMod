# British English Translation Pack

Hi,

I'm the person who made the alterations to the script found within this pack.  
I hope you enjoy the game.

## Important Notice

If you come across any errors when playing, I am truly sorry: just remember that I did effectively edit an entire novella by myself, all free of charge; and that this is **many, many times better than the original!** For various reasons which are outlined below, it will likely always contain a small amount of oversights. I'll keep trying to do better.
This pack is intended for any version of *1.0.7, or earlier*: Due to reasons explained below, some of my fixes may become errors themselves or simply cease to function should the developer push an update. But, as of the time of writing, it seems unlikely that the game will recieve any future updates. 

### Known Limitations

Also, some of them may not be my fault. As it currently stands, not all the text in the game is either able to be translated or is being so (you'll notice that a `need_translate` text file is generated to this affect), and some of the text displays differently in the game itself compared to the translation spreadsheet.


For example:
- Some ability data 
- Particularly some punctuation at the beginning and end of sentences, for which there is probably some associated flag, is added in by the game afterwards and stored elsewhere. Whatever the case, it can't be translated.

In other cases, either the game re-uses text or the injection method of translation always translates the same unique string of characters in the same manner.

For example:
- *'Chest'*. *'Chest'* is displayed when right-click examining a closed chest, when right-click examining an open chest, and in the UI when viewing a its contents. In any of these cases, a full-stop may or may not be automatically added. So its impossible to have the punctuation entirely consistant across all items and situations.
- *'Book'* - *'Book'* is displayed when right-click examining a book and in the inventory as a property of an item, e.g. sword, consumable, reagent, etc. In the former *'A book'* would be preferable, but *'Book'* in the latter. Again, a compromise is required.
- *'Continue', etc* - This is also true in dialogue options. Sometimes certain options will or will not have punctuation, full-stops, quotations, etc.
These examples should be sufficient to demonstrate that, in order to perform quality control, I had to not just *'translate'* but **play through the entire game to review every piece of text**. So i might have missed something!

For reasons currently unknown to me, there are some errors caused by the very presence of the translation tool: 
- It renders some examination text no longer functional, i.e. it cannot be clicked to read the information prompt. To circummvent this, temporarily alter the language back to *'English'* to read it.
- It causes **exception errors** (crashes) to occur in rare cases: e.g. clicking on the *'barkskinned'* tooltip, but only after another tooltip was previously opened. **Please save frequently!**

At this time, there is little I can do to remedy these. I hope to discover a solution for these in the future.

---

## Changes Made:

Preamble aside, the primary aim was to attempt to preserve the original work as much as possible, whilst also improving the consistency. 
In multiple cases, I could have rewritten large sections to *'improve'* them. But *'improve'* here would mean destroying the work under the assumption that my writing (style) is superior, correct, or preferred. This is arrogant, insulting, and destructive.
Here are the changes you can expect to see:

### Grammar, Spelling, and Punctuation Consistency
Fixed spelling and grammar errors, and on the whole improved punctuation (lots of commas added!):

- Removed some excessive use of multiple final puncutation marks (e.g. *!!!*, *?!?*).
- Improved the consistency of capitalisation after colons. The *'hidden'* added colons meant a review in the game's engine was required.
- Attempted to make all punctuation regarding and surrounding quotations consistent. Due to the aforementioned limitations, as well as my own oversights, some inconsistencies may remain. But as a whole, it is much improved.
- Because the game generates quotation marks (`"`) by itself, I chose to use these as the primary marks for quotations and apostrophes (`'`) for contained quotations; which some readers may consider against the 'British English style'. But it suffices.
- Changed many elipses to em-dashes. Where the intent was for the character's speech to be aprubtly broken off rather than slowly trailing off. The game doesn't support the em-dash character at this time, so a double hyphen was used instead.
- Some paragrahs that contained inappropriate punctuation that I could not removed have been adjusted to fit. For example:
	- An elipsis was at sentence end and couldn't be removed, the character speaks as if trailing off or is described as such. 
	- Other sections required similar adjustments, in some cases having to be shuffled around, have new sentences added, or just completely rewritten because of other mistakently placed quotations, full-stops, etc.
- And more.

### British English Conventions
- Altered spellings to British conventions (*traveller*, *colour*, *centre*, etc).
- Edited all hyphenated words to be more conventional - which contrary to what is typical for modern British English actually meant removing many.
- Altered anything I considered a gross Americanism to a British equivalent (e.g. '*real good*' to '*really good*').
- Altered some of the speaking dialogue to be more idiomatically British, without making the vernacular so strong that it cannot be understood by a wider audience.
- In the SKALD character creation, the player selects either male or female, man or woman. Most of the pronouns used in text in the game reflect this. In the cases where *'they'* was used to represent a known, single entity, it has been adjusted to use the appropriate second person pronoun to improve consistency.
	- I could also have changed this to say, body type A/B and changed many pronouns to they. But the aim of my changes is to maintain the intention of the original work and make as few adulterations as possible. Thus, my decision.

### Content Improvements
- Altered a few lines to provide additional context or clarity where potentially ambiguous text tended to cause uncertainty in some users.
- Added some other literary or cultural references to the text.
- Reduced the frequency of, or re-wrote some, text where the primary shortcoming was being too repetitive or awkward without intent. In these instances, they read as if they were written by either a tired man or someone who lacks diversity of expression in English.
Because other portions of the text are more expressive, I took a directive decision to make these alterations, but tried to retain the intent under the assumption they were intentionally thematically linked. For example:
	- Many uses of *'in any case'*, sometimes multiple in the same paragraph. 
	- multiple uses of words like *'conspiritorially'* (which itself had inconsistant spelling).
- Things which were simply errors. *'Cuts like a rapier'*. Despite the incredible diversity of rapiers and the difficultt in classifying them, it is reasonable to say that to most, they are a thrust-centric blade. The intended effect of this line is maintained by altering the blade or the verb.

### Literary References:
The text contains references to some more modern media, but I have added some literary references of my own, with little intrusion as possible. I did this just for fun; because it maintained my motivation. I hope they are found tasteful and are themeatically appropriate.
But they require much more effort than just skimming the text for errors, for instance re-reading other literature, and they often deviate too much from the original work to be reasonable.
For now, they include:
- **Gilbert & Sullivan**
- **Orwell**
- **Plutarc/Phyrrus**
- **Roald Dahl**
- **Shakespeare**
- **"We're Going on a Bearhunt"**
- **Antonia Barber - "The Mousehole Cat"**
- **Tolkein"

### Future Plans and Potential Improvements
I would like to add more literary references in the future, especially:
- **Monty Python**
- **Tolkien** (additional)
- **Other British works**

Find out why some stuff doesn't seem to want to translate!

Tremendous effort was made in producing the translation sample. However, it is incomplete and, from reading it, evident that it was organised as it was being produced. 
	- Some adjustments have already been made as I made further additions, but this labourious and tedious task is unfinished. With infinite time, I would complete and restructure it for the benefit of others to use in their translations.
	
---

## Final Thoughts

And that's about it. Just remember, even if many of the lines remain unaltered, I still had to read every single one and decide what best to do with it!

**Thank you for your time and understanding.** 