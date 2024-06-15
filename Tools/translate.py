import asyncio
import os
import sys
import xml.etree.ElementTree as ET
from pathlib import Path
from time import time

import aiofiles
import openai

# Define structure
BOILERPLATE = r"""<?xml version="1.0" encoding="utf-8"?>
<LanguageData></LanguageData>"""

# Directory structure and language details
LOCALES_DIR = Path("Languages/")
SOURCE_LANGUAGE = "English"
TARGET_LANGUAGES: list[str] = [
    "Arabic",
    "ChineseSimplified",
    "ChineseTraditional",
    "Czech",
    "Danish",
    "Dutch",
    "Estonian",
    "Finnish",
    "French",
    "German",
    "Hungarian",
    "Italian",
    "Japanese",
    "Korean",
    "Norwegian",
    "Polish",
    "Portuguese",
    "PortugueseBrazilian",
    "Romanian",
    "Russian",
    "Slovak",
    "Spanish",
    "SpanishLatin",
    "Swedish",
    "Turkish",
    "Ukrainian",
]

# Set up OpenAI API key
api_key = os.getenv("OPENAI_API_KEY")
client = openai.AsyncOpenAI(api_key=api_key)

# Define the rate limit (requests per minute)
REQUESTS_PER_MINUTE = 4000
REQUEST_INTERVAL = 60.0 / REQUESTS_PER_MINUTE
semaphore = asyncio.Semaphore(REQUESTS_PER_MINUTE)
last_request_time = 0


# Asynchronous translation function
async def translate_text(text: str, target_language: str) -> str:
    global last_request_time
    async with semaphore:
        current_time = time()
        elapsed_time = current_time - last_request_time
        if elapsed_time < REQUEST_INTERVAL:
            await asyncio.sleep(REQUEST_INTERVAL - elapsed_time)

        last_request_time = time()
    try:
        response = await client.chat.completions.create(
            model="gpt-4o",
            messages=[
                {
                    "role": "system",
                    "content": f"You are a Translator. For a given input text, output ONLY the {target_language} equivalent.\n\nFrench Example:\n\nInput:\nYou are a technical writer. Summarize the $num most relevant nodes from the provided XML to pass on to creative writers.\n\nOutput:\nVous êtes rédacteur technique. Résumez les $num nœuds les plus pertinents du XML fourni pour les transmettre aux rédacteurs créatifs.",
                },
                {"role": "user", "content": text},
            ],
        )
        return response.choices[0].message.content.strip()
    except Exception as e:
        print(f"Error translating text to {target_language}: {e}", file=sys.stderr)
        return text


# Function to handle XML file translation
async def process_xml_file(
    source_xml_file: Path,
    target_xml_file: Path,
    target_language: str,
    indices: list[int] | None = None,
):
    try:
        source_tree = ET.parse(source_xml_file)
        source_root = source_tree.getroot()
        target_tree = ET.parse(target_xml_file)
        target_root = target_tree.getroot()
    except Exception as e:
        print(f"Error reading .xml file: {e}", file=sys.stderr)
        return
    count: int = 0

    # Ensure elements
    existing_tags = {el.tag for el in target_root.findall(".//")}
    for element in source_root.findall(".//"):
        if element.tag not in existing_tags:
            target_root.append(ET.Element(element.tag))

    # Do translations
    lang_items = [element for element in target_root.findall(".//")]
    errors: list[str] = []
    for element in lang_items:
        try:
            source_element = source_root.find(element.tag)
            if source_element is None or source_element.text is None:
                raise ValueError("Missing source text or element.")
            if indices is not None:
                if count in indices and element.text is not None:
                    translated_text = await translate_text(
                        source_element.text, target_language
                    )
                    element.text = translated_text
            else:
                translated_text = await translate_text(
                    source_element.text, target_language
                )
                element.text = translated_text
        except Exception as e:
            errors.append(f"Failed to translate {element.tag}: {str(e)}")
            continue
        finally:
            count += 1
    if errors:
        print("Some translations were not completed due to errors:", file=sys.stderr)
        for error in errors:
            print(error, file=sys.stderr)

    # Write translated XML to the appropriate file in the target language directory
    async with aiofiles.open(target_xml_file, "w", encoding="utf-8") as f:
        await f.write(
            ET.tostring(
                target_root,
                encoding="utf-8",
                xml_declaration=True,
            ).decode("utf-8")
        )

    # Done
    print(f"Language {target_language} finished!")


# Process all .xml files in the directory
async def process_xml_files(languages_dir: Path, indices: list[int] | None = None):
    source_dir = languages_dir / SOURCE_LANGUAGE / Path("Keyed")
    source_keyed_files = [xml_file for xml_file in source_dir.rglob("*.xml")]
    tasks = []

    # Work files
    for lang in TARGET_LANGUAGES:
        target_dir: Path = languages_dir / lang / Path("Keyed")
        if not target_dir.exists():
            target_dir.mkdir(parents=True, exist_ok=True)
        for source_xml_file in source_keyed_files:
            target_xml_file = target_dir / source_xml_file.name
            if not target_xml_file.exists():
                target_xml_file.write_text(BOILERPLATE)
            tasks.append(
                process_xml_file(
                    source_xml_file,
                    target_xml_file,
                    lang,
                    indices=indices if indices is not None else None,
                )
            )

    # Gather tasks
    await asyncio.gather(*tasks)


# Main entry point
if __name__ == "__main__":
    # Example usage: redo translations for French language, indices 0 and 2
    # asyncio.run(redo_translations("fr", [1, 2]))

    # Process all files
    asyncio.run(process_xml_files(LOCALES_DIR))
