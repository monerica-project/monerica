# Monerica

A Directory For A Monero Circular Economy
Hosted at [monerica.org](https://monerica.org)


# Developing

`git clone`

`npm i`

`npm run dev`

Remember to take a look at the build of the site before pushing a commit
`npm run build`

`npm run preview`

# Adding a site

Don't be scared of all the code! Adding a site is as easy as editing the DIRECTORY.yaml file. You don't need any
coding experience. Here are the steps:

1. Assuming you're viewing this doc on github, press the period on your keyboard `.`
2. This will open a code editor in the browser, click on `DIRECTORY.yaml` to open it
3. Add your site to this file, just follow the formatting of the other sites
4. Click the "source control" button on the left-hand side, then click "create pull request"

# Goals:

1. Easy, streamlined contributions for users and devs
2. Hightly performant page that works without JavaScript.
3. No server to make hosting and distribution easier

# Design Rationale

## Why Svelte?

Because it's a resonably popular framework that works well as a Static Site Generator. It will
also not limit future enhancements.

## Why a giant yaml?

It's more human-readable than JSON while able to be easily parsed into a JS-friendly object.
TOML doesn't support enough nesting and has less robust strings. Markdown has less flexibility and is
harder to parse. XML is ugly. It's easy to refactor into multiple files. It makes it easy for
non-technical users to contribute

# TODO:

1. Add deployments for TOR and various decentralized web hosting services. We want something to fallback on in case the cloudflare site gets attacked. I'll have to configure dns failover or something to intelligently resolve the domain.
2. add better styling
3. Create CI/CD for all deployment endpoints
